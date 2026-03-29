using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Signage.Common;

namespace Signage.Server.Services;

public class SecurityService
{
    private const string UsersCategory = "users";
    private const string AuditLogsCategory = "audit-logs";
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AuditLogRetention = TimeSpan.FromDays(365);
    private static readonly TimeSpan AuditCleanupInterval = TimeSpan.FromHours(12);
    private readonly SqliteJsonStore _store;
    private List<UserAccount> _users = new();
    private List<AuditLogEntry> _auditLogs = new();
    private readonly Dictionary<string, CaptchaChallenge> _captchaStore = new();
    private readonly ILogger<SecurityService> _logger;
    private DateTime _lastAuditCleanupUtc = DateTime.MinValue;

    public SecurityService(ILogger<SecurityService> logger, SqliteJsonStore store)
    {
        _logger = logger;
        _store = store;
        _users = _store.LoadAll<UserAccount>(UsersCategory);
        _auditLogs = _store.LoadAll<AuditLogEntry>(AuditLogsCategory)
            .OrderBy(a => a.TimestampUtc)
            .ToList();

        EnsureAuditRetention(force: true);

        if (!_users.Any())
        {
            InitializeDefaultAdmin();
        }
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
            PasswordChangedAtUtc = u.PasswordChangedAtUtc,
            FailedLoginCount = u.FailedLoginCount,
            LockoutUntilUtc = u.LockoutUntilUtc,
            IsMfaEnabled = u.IsMfaEnabled
        }).ToList();
    }

    public (bool success, string message) ToggleUserActive(string targetUsername, string actorUsername)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在");

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && actorUsername.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return (false, "不可停用系統管理員帳號");

        user.IsActive = !user.IsActive;
        PersistUsers();
        AddAudit(actorUsername, user.IsActive ? "User.Enable" : "User.Disable", targetUsername, true, user.IsActive ? "啟用帳號" : "停用帳號", "", "");
        return (true, user.IsActive ? "帳號已啟用" : "帳號已停用");
    }

    public (bool success, string message) AdminResetPassword(string targetUsername, string newPassword, string actorUsername)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在");

        var policyResult = ValidatePasswordPolicy(newPassword);
        if (!policyResult.success)
            return (false, policyResult.message);

        ApplyPassword(user, newPassword);
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        PersistUsers();
        AddAudit(actorUsername, "User.AdminResetPassword", targetUsername, true, "管理員重設密碼", "", "");
        return (true, "密碼重設成功");
    }

    public (bool success, string message) UpdateUserRole(string targetUsername, string newRole, string actorUsername)
    {
        if (!UserRoles.All.Contains(newRole))
            return (false, "角色不合法");

        var user = _users.FirstOrDefault(u => u.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在");

        var oldRole = user.Role;
        user.Role = newRole;
        PersistUsers();
        AddAudit(actorUsername, "User.UpdateRole", targetUsername, true, $"角色由 {oldRole} 改為 {newRole}", "", "");
        return (true, "角色更新成功");
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
    PersistUsers();
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

        // 開發環境臨時調試：允許任何驗證碼通過（正式環境應移除此部分）
        if (string.IsNullOrWhiteSpace(captchaCode))
        {
            _logger.LogWarning("登入失敗：未填寫圖形驗證碼，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", "", false, "未填寫圖形驗證碼", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "請填寫圖形驗證碼" };
        }

        // 驗證碼驗證：如果驗證碼存在於字典，則進行驗證；否則允許通過（開發模式）
        if (_captchaStore.TryGetValue(captchaId, out var challenge) && challenge.ExpiresAtUtc >= DateTime.UtcNow)
        {
            _captchaStore.Remove(captchaId);
            if (!string.Equals(challenge.ExpectedCode, captchaCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("登入失敗：圖形驗證錯誤，帳號={Username}, IP={Ip}", username, ipAddress);
                AddAudit(username, "Auth.Login", "", false, "圖形驗證錯誤", ipAddress, userAgent);
                return new AuthResult { Success = false, Message = "圖形驗證碼錯誤" };
            }
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

        // 帳號鎖定檢查
        if (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntilUtc.Value - DateTime.UtcNow).TotalMinutes);
            _logger.LogWarning("登入失敗：帳號鎖定，帳號={Username}, IP={Ip}", username, ipAddress);
            AddAudit(username, "Auth.Login", user.Username, false, "帳號已鎖定", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = $"帳號已鎖定，請於 {remaining} 分鐘後再試" };
        }

        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("帳號鎖定：失敗{Count}次，帳號={Username}, IP={Ip}", user.FailedLoginCount, username, ipAddress);
                AddAudit(username, "Auth.Login", user.Username, false, $"密碼錯誤達 {MaxFailedAttempts} 次，帳號已鎖定", ipAddress, userAgent);
                PersistUsers();
                return new AuthResult { Success = false, Message = $"密碼錯誤次數過多，帳號已鎖定 {(int)LockoutDuration.TotalMinutes} 分鐘" };
            }

            _logger.LogWarning("登入失敗：密碼錯誤（第{Count}次），帳號={Username}, IP={Ip}", user.FailedLoginCount, username, ipAddress);
            AddAudit(username, "Auth.Login", user.Username, false, $"密碼錯誤（第 {user.FailedLoginCount} 次）", ipAddress, userAgent);
            PersistUsers();
            return new AuthResult { Success = false, Message = "帳號或密碼錯誤" };
        }

        // 登入成功，重置失敗計數
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        PersistUsers();

        // MFA 驗證流程：若啟用則需進行第二因素驗證
        if (user.IsMfaEnabled)
        {
            AddAudit(username, "Auth.Login", user.Username, true, "密碼驗證通過，等待 MFA", ipAddress, userAgent);
            return new AuthResult
            {
                Success = true,
                RequiresMfa = true,
                Message = "請輸入雙因素驗證碼",
                User = user
            };
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

    public AuthResult VerifyMfaCode(string userId, string totpCode, string ipAddress, string userAgent)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user is null || !user.IsMfaEnabled || string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            AddAudit(userId, "Auth.MfaVerify", "", false, "MFA 驗證失敗：帳號不存在或未啟用", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "MFA 驗證失敗" };
        }

        if (!VerifyTotp(user.TotpSecret, totpCode))
        {
            _logger.LogWarning("MFA 驗證失敗，UserId={UserId}, IP={Ip}", userId, ipAddress);
            AddAudit(user.Username, "Auth.MfaVerify", user.Username, false, "TOTP 碼不正確", ipAddress, userAgent);
            return new AuthResult { Success = false, Message = "雙因素驗證碼錯誤" };
        }

        var expired = user.PasswordChangedAtUtc.AddDays(180) <= DateTime.UtcNow;
        AddAudit(user.Username, "Auth.MfaVerify", user.Username, true, "MFA 驗證成功", ipAddress, userAgent);
        return new AuthResult
        {
            Success = true,
            Message = expired ? "登入成功，但密碼已超過 180 天需更新" : "登入成功",
            RequiresPasswordChange = expired,
            User = user
        };
    }

    public (bool success, string message, string? qrCodeUri, string? secret) SetupMfa(string username, string actorUsername)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在", null, null);

        var secret = GenerateTotpSecret();
        user.TotpSecret = secret;
        user.IsMfaEnabled = false; // 需驗證後才正式啟用
        PersistUsers();

        var otpUri = $"otpauth://totp/SignageSystem:{Uri.EscapeDataString(username)}?secret={secret}&issuer=SignageSystem&digits=6&period=30";
        AddAudit(actorUsername, "Auth.MfaSetup", username, true, "建立 MFA 金鑰（未啟用）", "", "");
        return (true, "請使用 Authenticator App 掃描 QR Code 並驗證", otpUri, secret);
    }

    public (bool success, string message) ConfirmMfa(string username, string totpCode, string actorUsername)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null || string.IsNullOrWhiteSpace(user.TotpSecret))
            return (false, "帳號不存在或尚未初始化 MFA");

        if (!VerifyTotp(user.TotpSecret, totpCode))
            return (false, "驗證碼錯誤，請確認 App 時間正確");

        user.IsMfaEnabled = true;
        PersistUsers();
        AddAudit(actorUsername, "Auth.MfaConfirm", username, true, "MFA 啟用成功", "", "");
        return (true, "雙因素驗證已啟用");
    }

    public (bool success, string message) DisableMfa(string username, string actorUsername)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
            return (false, "帳號不存在");

        user.IsMfaEnabled = false;
        user.TotpSecret = null;
        PersistUsers();
        AddAudit(actorUsername, "Auth.MfaDisable", username, true, "MFA 停用", "", "");
        return (true, "雙因素驗證已停用");
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
    PersistUsers();
        AddAudit(username, "Auth.ChangePassword", username, true, "密碼更新成功", ipAddress, userAgent);
        return (true, "密碼更新成功");
    }

    public IEnumerable<AuditLogEntry> GetAuditLogs(int limit = 200)
    {
        return GetAuditLogs(new AuditLogQuery { Limit = limit });
    }

    public IEnumerable<AuditLogEntry> GetAuditLogs(AuditLogQuery? query)
    {
        EnsureAuditRetention();

        var effectiveQuery = query ?? new AuditLogQuery();
        var normalizedLimit = NormalizeLimit(effectiveQuery.Limit);

        var filtered = _auditLogs.AsEnumerable();

        if (effectiveQuery.FromUtc.HasValue)
            filtered = filtered.Where(item => item.TimestampUtc >= effectiveQuery.FromUtc.Value);

        if (effectiveQuery.ToUtc.HasValue)
            filtered = filtered.Where(item => item.TimestampUtc <= effectiveQuery.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(effectiveQuery.Username))
        {
            var keyword = effectiveQuery.Username.Trim();
            filtered = filtered.Where(item => item.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(effectiveQuery.Action))
        {
            var keyword = effectiveQuery.Action.Trim();
            filtered = filtered.Where(item => item.Action.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (effectiveQuery.Success.HasValue)
            filtered = filtered.Where(item => item.Success == effectiveQuery.Success.Value);

        return filtered
            .OrderByDescending(item => item.TimestampUtc)
            .Take(normalizedLimit)
            .ToList();
    }

    public string BuildAuditLogsCsv(AuditLogQuery? query)
    {
        var rows = GetAuditLogs(query).ToList();

        var csv = new StringBuilder();
        csv.AppendLine("TimestampUtc,Username,Action,Target,Success,IpAddress,Detail");

        foreach (var row in rows)
        {
            csv.Append(CsvEscape(row.TimestampUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',');
            csv.Append(CsvEscape(row.Username)).Append(',');
            csv.Append(CsvEscape(row.Action)).Append(',');
            csv.Append(CsvEscape(row.Target)).Append(',');
            csv.Append(row.Success ? "true" : "false").Append(',');
            csv.Append(CsvEscape(row.IpAddress)).Append(',');
            csv.AppendLine(CsvEscape(row.Detail));
        }

        return csv.ToString();
    }

    public void AddAudit(string username, string action, string target, bool success, string detail, string ipAddress, string userAgent)
    {
        EnsureAuditRetention();

        var entry = new AuditLogEntry
        {
            Username = string.IsNullOrWhiteSpace(username) ? "anonymous" : username,
            Action = action,
            Target = target,
            Success = success,
            Detail = detail,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TimestampUtc = DateTime.UtcNow
        };

        // 計算日誌完整性雜湊鏈
        var previousHash = _auditLogs.Count > 0 ? _auditLogs[^1].IntegrityHash : "GENESIS";
        entry.IntegrityHash = ComputeAuditEntryHash(previousHash, entry);

        _auditLogs.Add(entry);

        if (_auditLogs.Count > 5000)
        {
            _auditLogs.RemoveRange(0, 500);
            RebuildAuditIntegrityHashes();
        }

        PersistAuditLogs();
    }

    public bool VerifyAuditLogIntegrity()
    {
        var previousHash = "GENESIS";
        foreach (var entry in _auditLogs.OrderBy(a => a.TimestampUtc))
        {
            var expected = ComputeAuditEntryHash(previousHash, entry);
            if (expected != entry.IntegrityHash)
                return false;
            previousHash = entry.IntegrityHash;
        }
        return true;
    }

    private static string ComputeAuditEntryHash(string previousHash, AuditLogEntry entry)
    {
        var content = $"{previousHash}|{entry.TimestampUtc:O}|{entry.Username}|{entry.Action}|{entry.Target}|{entry.Success}|{entry.Detail}|{entry.IpAddress}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
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

    private static string GenerateTotpSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        // Base32 編碼（RFC 4648）
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder();
        var bitsLeftover = 0;
        var currentByte = 0;
        foreach (var b in bytes)
        {
            currentByte = (currentByte << 8) | b;
            bitsLeftover += 8;
            while (bitsLeftover >= 5)
            {
                bitsLeftover -= 5;
                sb.Append(base32Chars[(currentByte >> bitsLeftover) & 0x1F]);
            }
        }
        if (bitsLeftover > 0)
            sb.Append(base32Chars[(currentByte << (5 - bitsLeftover)) & 0x1F]);
        return sb.ToString();
    }

    private static bool VerifyTotp(string base32Secret, string userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode) || userCode.Length != 6)
            return false;

        if (!int.TryParse(userCode, out var code))
            return false;

        var keyBytes = Base32Decode(base32Secret);
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 允許前後一個時間視窗（30 秒）容忍時鐘偏差
        for (var drift = -1; drift <= 1; drift++)
        {
            var counter = (unixTime / 30) + drift;
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(keyBytes);
            var hash = hmac.ComputeHash(counterBytes);
            var offset = hash[^1] & 0x0F;
            var truncated = ((hash[offset] & 0x7F) << 24)
                          | ((hash[offset + 1] & 0xFF) << 16)
                          | ((hash[offset + 2] & 0xFF) << 8)
                          | (hash[offset + 3] & 0xFF);
            var otp = truncated % 1_000_000;
            if (otp == code)
                return true;
        }
        return false;
    }

    private static byte[] Base32Decode(string input)
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpperInvariant();
        var outputLength = input.Length * 5 / 8;
        var result = new byte[outputLength];
        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;
        foreach (var c in input)
        {
            var charIndex = base32Chars.IndexOf(c);
            if (charIndex < 0) continue;
            buffer = (buffer << 5) | charIndex;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result[index++] = (byte)(buffer >> bitsLeft);
            }
        }
        return result;
    }

    private void EnsureAuditRetention(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - _lastAuditCleanupUtc < AuditCleanupInterval)
            return;

        _lastAuditCleanupUtc = now;
        var cutoff = now - AuditLogRetention;
        var originalCount = _auditLogs.Count;

        _auditLogs = _auditLogs
            .Where(item => item.TimestampUtc >= cutoff)
            .OrderBy(item => item.TimestampUtc)
            .ToList();

        var changed = force || originalCount != _auditLogs.Count;
        if (!changed)
            return;

        RebuildAuditIntegrityHashes();
        PersistAuditLogs();
    }

    private void RebuildAuditIntegrityHashes()
    {
        var previousHash = "GENESIS";
        foreach (var entry in _auditLogs.OrderBy(item => item.TimestampUtc))
        {
            entry.IntegrityHash = ComputeAuditEntryHash(previousHash, entry);
            previousHash = entry.IntegrityHash;
        }
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0)
            return 200;

        return Math.Min(limit, 5000);
    }

    private static string CsvEscape(string value)
    {
        value ??= string.Empty;
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private void InitializeDefaultAdmin()
    {
        var result = CreateUser("admin", "System Admin", UserRoles.Admin, "Admin@123", "system");
        if (result.success)
            AddAudit("system", "System.Init", "admin", true, "建立預設系統管理員帳號", "", "");
    }

    private void PersistUsers()
    {
        _store.ReplaceAll(UsersCategory, _users, item => item.Id);
    }

    private void PersistAuditLogs()
    {
        _store.ReplaceAll(AuditLogsCategory, _auditLogs, item => item.Id);
    }
}
