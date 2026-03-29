namespace Signage.Common;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Operator, Viewer];
}

public class PasswordHistoryEntry
{
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public class UserAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = UserRoles.Viewer;

    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public DateTime PasswordChangedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";

    public List<PasswordHistoryEntry> PasswordHistory { get; set; } = new();

    // 帳號鎖定
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutUntilUtc { get; set; }

    // TOTP 多重認證
    public string? TotpSecret { get; set; }
    public bool IsMfaEnabled { get; set; } = false;
}

public class CaptchaChallenge
{
    public string CaptchaId { get; set; } = Guid.NewGuid().ToString();
    public string ExpectedCode { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}

public class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Username { get; set; } = "anonymous";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Success { get; set; }
    public string Detail { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    // 日誌完整性雜湊鏈（SHA256 of previous hash + current entry content）
    public string IntegrityHash { get; set; } = "";
}

public class AuditLogQuery
{
    public int Limit { get; set; } = 200;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Username { get; set; }
    public string? Action { get; set; }
    public bool? Success { get; set; }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool RequiresPasswordChange { get; set; }
    public bool RequiresMfa { get; set; }
    public UserAccount? User { get; set; }
}
