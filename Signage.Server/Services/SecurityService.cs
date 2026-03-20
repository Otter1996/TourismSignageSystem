using System.Security.Cryptography;
using System.Text;
using Signage.Common;

namespace Signage.Server.Services;

public class SecurityService
{
    private readonly List<UserAccount> _users = new();
    private readonly List<AuditLogEntry> _auditLogs = new();
    private readonly Dictionary<string, CaptchaChallenge> _captchaStore = new();
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        InitializeDefaultAdmin();
    }

    public IEnumerable<UserAccount> GetUsers()
    {
        return _users.Select(u => new UserAccount
        {
            Id = u.Id,
            Username = u.Username,
            DisplayName = u.DisplayName,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAtUtc = u.CreatedAtUtc,
            CreatedBy = u.CreatedBy,
            PasswordChangedAtUtc = u.PasswordChangedAtUtc
        }).ToList();
    }

    public (bool success, string message, UserAccount? user) CreateUser(string username, string displayName, string role, string password, string createdBy)
    {
        username = username.Trim();
        if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return (false, "帳號已存在", null);

        if (!UserRoles.All.Contains(role))
            return (false, "角色不合法", null);

        var policyResult = ValidatePasswordPolicy(password);
        if (!policyResult.success)
            return (false, policyResult.message, null);

        var (hash, salt) = HashPassword(password);
        var user = new UserAccount
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim(),
            Role = role,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordChangedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsActive = true,
            PasswordHistory = new List<PasswordHistoryEntry>
            {
                new()
                {
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    ChangedAt = DateTime.UtcNow
                }
            }
        };

        _users.Add(user);
        AddAudit(createdBy, "User.Create", username, true, $"建立角色 {role}", "", "");
        return (true, "建立成功", user);
    }

    public (string captchaId, string imageBase64) CreateCaptcha()
    {
        var code = GenerateCaptchaCode(5);
        var challenge = new CaptchaChallenge
        {
            CaptchaId = Guid.NewGuid().ToString("N"),
            ExpectedCode = code,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };

        _captchaStore[challenge.CaptchaId] = challenge;

        var svg = BuildCaptchaSvg(code);
        var bytes = Encoding.UTF8.GetBytes(svg);
        return (challenge.CaptchaId, Convert.ToBase64String(bytes));
    }

    public AuthResult Authenticate(string username, string password, string captchaId, string captchaCode, string ipAddress, string userAgent)
    {
        CleanupExpiredCaptcha();

        if (!_captchaStore.TryGetValue(captchaId, out var challenge) || challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("登入失敗：圖形驗證逾期或不存在，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", "", false, "圖形驗證逾期或不存在", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "圖形驗證已過期，請重新取得" };
        }

        _captchaStore.Remove(captchaId);

        if (!string.Equals(challenge.ExpectedCode, captchaCode?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("登入失敗：圖形驗證錯誤，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", "", false, "圖形驗證錯誤", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "圖形驗證碼錯誤" };
        }

        var user = _users.FirstOrDefault(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            _logger.LogWarning("登入失敗：帳號不存在，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", "", false, "帳號不存在", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "帳號或密碼錯誤" };
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("登入失敗：帳號停用，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", user.Username, false, "帳號停用", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "帳號停用" };
        }

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("登入失敗：密碼錯誤，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", user.Username, false, "密碼錯誤", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "帳號或密碼錯誤" };
        }

        var expired = user.PasswordChangedAtUtc.AddDays(180) <= DateTime.UtcNow;
        AddAudit(username, "Auth.Login", user.Username, true, expired ? "登入成功，需更新密碼" : "登入成功", ipAddress, userAgent);
        return new AuthResult
        {
            Success = true,
            Message = expired ? "登入成功，但密碼已超過 180 天需更新" : "登入成功",
            RequiresPasswordChange = expired,
            User = user
        };
    }

    public (bool success, string message) ChangePassword(string username, string currentPassword, string newPassword, string ipAddress, string userAgent)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在");

        if (!VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("變更密碼失敗：舊密碼錯誤，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.ChangePassword", username, false, "舊密碼錯誤", ipAddress, userAgent);
            return (false, "舊密碼錯誤");
        }

        var policyResult = ValidatePasswordPolicy(newPassword);
        if (!policyResult.success)
            return (false, policyResult.message);

        if (IsSameAsRecentPasswords(user, newPassword))
            return (false, "新密碼不可與前三組密碼相同");

        ApplyPassword(user, newPassword);
        AddAudit(username, "Auth.ChangePassword", username, true, "密碼更新成功", ipAddress, userAgent);
        return (true, "密碼更新成功");
    }

    public IEnumerable<AuditLogEntry> GetAuditLogs(int limit = 200)
    {
        return _auditLogs.OrderByDescending(a => a.TimestampUtc).Take(limit).ToList();
    }

    public void AddAudit(string username, string action, string target, bool success, string detail, string ipAddress, string userAgent)
    {
        _auditLogs.Add(new AuditLogEntry
        {
            Username = string.IsNullOrWhiteSpace(username) ? "anonymous" : username,
            Action = action,
            Target = target,
            Success = success,
            Detail = detail,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TimestampUtc = DateTime.UtcNow
        });

        if (_auditLogs.Count > 5000)
            _auditLogs.RemoveRange(0, 500);
    }

    private static (bool success, string message) ValidatePasswordPolicy(string password)
    {
        if (password.Length < 8)
            return (false, "密碼長度至少 8 碼");

        if (!password.Any(char.IsUpper))
            return (false, "密碼需至少包含一個大寫字母");

        if (!password.Any(char.IsLower))
            return (false, "密碼需至少包含一個小寫字母");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            return (false, "密碼需至少包含一個特殊符號");

        return (true, "OK");
    }

    private static bool IsSameAsRecentPasswords(UserAccount user, string candidatePassword)
    {
        var recentThree = user.PasswordHistory
            .OrderByDescending(p => p.ChangedAt)
            .Take(3)
            .ToList();

        return recentThree.Any(h => VerifyPassword(candidatePassword, h.PasswordHash, h.PasswordSalt));
    }

    private static void ApplyPassword(UserAccount user, string newPassword)
    {
        var (hash, salt) = HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.PasswordChangedAtUtc = DateTime.UtcNow;

        user.PasswordHistory.Insert(0, new PasswordHistoryEntry
        {
            PasswordHash = hash,
            PasswordSalt = salt,
            ChangedAt = DateTime.UtcNow
        });

        user.PasswordHistory = user.PasswordHistory
            .OrderByDescending(p => p.ChangedAt)
            .Take(3)
            .ToList();
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string expectedHash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        var actual = Convert.ToBase64String(hashBytes);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(expectedHash));
    }

    private static string GenerateCaptchaCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(chars[bytes[i] % chars.Length]);
        return sb.ToString();
    }

    private static string BuildCaptchaSvg(string code)
    {
        var noise1 = RandomNumberGenerator.GetInt32(5, 25);
        var noise2 = RandomNumberGenerator.GetInt32(10, 35);
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='180' height='60'>"
               + "<rect width='100%' height='100%' fill='#f3f4f6'/>"
               + $"<line x1='10' y1='{noise1}' x2='170' y2='{60 - noise1}' stroke='#9ca3af' stroke-width='2'/>"
               + $"<line x1='10' y1='{noise2}' x2='170' y2='{60 - noise2}' stroke='#6b7280' stroke-width='1.5'/>"
               + $"<text x='20' y='40' font-size='32' font-family='monospace' fill='#111827' letter-spacing='6'>{code}</text>"
               + "</svg>";
    }

    private void CleanupExpiredCaptcha()
    {
        var expiredKeys = _captchaStore
            .Where(pair => pair.Value.ExpiresAtUtc < DateTime.UtcNow)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredKeys)
            _captchaStore.Remove(key);
    }

    private void InitializeDefaultAdmin()
    {
        var result = CreateUser("admin", "System Admin", UserRoles.Admin, "Admin@123", "system");
        if (result.success)
            AddAudit("system", "System.Init", "admin", true, "建立預設系統管理員帳號", "", "");
    }
}
