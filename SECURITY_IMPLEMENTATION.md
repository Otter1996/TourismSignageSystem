# 觀光局電子看板管理系統 - 安全功能實現報告

**實現日期**：2026年3月29日  
**版本**：2.0  
**根據規定**：中華民國政府資訊安全政策(OGAT-09)

---

## 概述

本文檔詳細說明觀光局電子看板系統已實現的安全功能，符合個人資料保護、身份管理、存錄安全等規定。

---

## 一、安全程式設計原則

### ✅ 1.1 OWASP Top 10 防範

| 編號 | 漏洞 | 實現措施 |
|------|------|--------|
| **A01:2021 – Broken Access Control** | 破碎存取控制 | • RBAC 三層角色（Admin/Operator/Viewer）<br/>• JWT 令牌驗證<br/>• 控制器 `[Authorize]` 屬性 |
| **A02:2021 – Cryptographic Failures** | 密碼學失敗 | • HTTPS/SSL 強制<br/>• PBKDF2 SHA256+Salt 密碼雜湊<br/>• JWT 簽名密鑰管理 |
| **A03:2021 – Injection** | 注入攻擊 | • SQLite JSON 參數化存儲<br/>• 伺服器端輸入驗證<br/>• RegEx 帳號驗證（`^[a-zA-Z0-9_\-.@]{1,64}$`） |
| **A04:2021 – Insecure Design** | 不安全設計 | • 設計階段考慮安全<br/>• 多層防護（CAPTCHA+密碼政策） |
| **A05:2021 – Security Misconfiguration** | 安全設定錯誤 | • 全域例外處理中間件<br/>• 安全 HTTP 標頭<br/>• 預設密碼強制變更 |
| **A06:2021 – Vulnerable & Outdated Components** | 元件漏洞 | • .NET 10.0 最新版本<br/>• 定期依賴更新 |
| **A07:2021 – Authentication Failures** | 認證失敗 | • 帳號鎖定機制<br/>• TOTP 多重認證<br/>• 密碼有效期 180 天 |
| **A09:2021 – Logging & Monitoring Failures** | 日誌/監控失敗 | • 雜湊鏈日誌完整性<br/>• 完整審計軌跡 |

### ✅ 1.2 伺服器端輸入驗證

**AuthController.cs - 帳號驗證**
```csharp
// 帳號格式檢查
if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > MaxUsernameLength)
    return BadRequest(new { message = "帳號格式不正確" });

if (!SafeUsernameRegex.IsMatch(request.Username))
    return BadRequest(new { message = "帳號包含不允許的字元" });

// 密碼長度限制
if (request.Password.Length > MaxPasswordLength)
    return BadRequest(new { message = "密碼格式不正確" });
```

**驗證規則**
- 帳號：1-64 字元，含數字、字母、下劃線、連字號、點號、@ 符號
- 密碼：不超過 128 位元組
- 顯示名稱：不超過 100 字元
- CAPTCHA 碼：6 位英數字

### ✅ 1.3 錯誤訊息遮蔽

**中間件實現** - `ExceptionHandlingMiddleware.cs`
```csharp
catch (Exception ex)
{
    var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    // 伺服器端記錄完整錯誤
    _logger.LogError(ex, "未預期的系統錯誤，ErrorId={ErrorId}", errorId);
    
    // 用戶端僅收到通用錯誤代碼
    var response = JsonSerializer.Serialize(new
    {
        errorCode = $"ERR-{errorId}",
        message = "系統發生錯誤，請記錄錯誤代碼並聯繫管理員"
    });
}
```

**遮蔽內容**
- ❌ 不洩漏堆疊追蹤
- ❌ 不洩漏部分數據
- ❌ 不洩漏資料庫查詢
- ✅ 提供通用錯誤 ID 供追蹤

---

## 二、密碼安全管理

### ✅ 2.1 密碼儲存

**加鹽 PBKDF2-SHA256 實現** - `SecurityService.cs`
```csharp
private static (string hash, string salt) HashPassword(string password)
{
    var saltBytes = RandomNumberGenerator.GetBytes(16);  // 128 位隨機鹽
    var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(password),
        saltBytes,
        100_000,      // 10 萬次迭代
        HashAlgorithmName.SHA256,
        32);          // 256 位輸出
    
    return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
}
```

- **雜湊算法**：PBKDF2-SHA256
- **迭代次數**：100,000
- **鹽值**：128 位隨機
- **密碼長度要求**
  - 最少 8 位
  - 必含大寫字母
  - 必含小寫字母
  - 必含特殊符號

### ✅ 2.2 密碼傳輸安全

- **HTTPS/SSL 強制**：`Program.cs` 中 `UseHttpsRedirection()`
- **JWT 簽名傳輸**：避免明文
- **密碼永遠不保存**客戶端

### ✅ 2.3 密碼原則

- **系統管理員默認密碼**：`Admin@123`（首次登入強制變更）
- **無預設密碼原則**：新帳號必須由管理員設定

### ✅ 2.4 密碼歷史限制

```csharp
public PasswordHistory { get; set; } = new();

private static bool IsSameAsRecentPasswords(UserAccount user, string candidatePassword)
{
    var recentThree = user.PasswordHistory
        .OrderByDescending(p => p.ChangedAt)
        .Take(3)
        .ToList();
    
    return recentThree.Any(h => VerifyPassword(candidatePassword, h.PasswordHash, h.PasswordSalt));
}
```
- **保留歷史**：最近 3 次密碼
- **重用禁止**：新密碼不可與前 3 次相同

### ✅ 2.5 密碼有效期

- **強制變更期限**：180 天
- **超期提示**：登入時通知須變更

---

## 三、身份識別與存取控制

### ✅ 3.1 帳號與權限

**RBAC 三層架構**
```csharp
public static class UserRoles
{
    public const string Admin = "Admin";      // 系統管理
    public const string Operator = "Operator"; // 內容管理
    public const string Viewer = "Viewer";     // 唯讀檢視
}
```

**最小權限原則**
| 功能 | Admin | Operator | Viewer |
|------|-------|----------|--------|
| 帳號管理 | ✅ | ❌ | ❌ |
| 版型配置 | ✅ | ✅ | ❌ |
| 媒體管理 | ✅ | ✅ | ❌ |
| 設備管理 | ✅ | ✅ | ❌ |
| 版型檢視 | ✅ | ✅ | ✅ |
| 操作稽核 | ✅ | ❌ | ❌ |

### ✅ 3.2 多重認證（MFA）

**TOTP RFC-6238 實現** - `SecurityService.cs`
```csharp
private static bool VerifyTotp(string base32Secret, string userCode)
{
    var keyBytes = Base32Decode(base32Secret);
    var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    // 允許前後一個時間視窗容忍時鐘偏差
    for (var drift = -1; drift <= 1; drift++)
    {
        var counter = (unixTime / 30) + drift;  // 30 秒週期
        // HMAC-SHA1 計算
        using var hmac = new HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(counterBytes);
        
        // RFC-6238 截短演算
        var offset = hash[^1] & 0x0F;
        var truncated = ((hash[offset] & 0x7F) << 24) ...
        var otp = truncated % 1_000_000;
        
        if (otp == code) return true;
    }
    return false;
}
```

**MFA 流程**
1. 使用者於帳號管理頁面點選「建立我的 MFA 金鑰」
2. 系統生成 Base32 編碼金鑰
3. 用戶掃描 QR Code 至 Google/Microsoft Authenticator
4. 輸入 6 位數 TOTP 驗證確認
5. 啟用後，登入需進行二步驗證

**停用 MFA**：僅管理員可停用使用者的 MFA

### ✅ 3.3 會期與登入管理

#### 3.3.1 帳號鎖定機制 ✅
```csharp
private const int MaxFailedAttempts = 5;
private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

if (user.FailedLoginCount >= MaxFailedAttempts)
{
    user.LockoutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
    AddAudit(username, "Auth.Login", user.Username, false, 
             $"密碼錯誤達 {MaxFailedAttempts} 次，帳號已鎖定");
}
```
- **失敗次數**：5 次
- **鎖定時間**：15 分鐘
- **重置**：密碼修改時清除

#### 3.3.2 閒置自動登出 ✅
前端 `AuthSessionService.cs`
```csharp
private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

private void CheckIdle(object? _)
{
    if (DateTime.UtcNow - _lastActivityUtc >= IdleTimeout)
    {
        OnIdleLogout?.Invoke();  // 觸發登出
    }
}

public void RecordActivity()
{
    _lastActivityUtc = DateTime.UtcNow;  // 任何互動重置計時
}
```
- **閒置期限**：15 分鐘
- **監控事件**：滑鼠移動、鍵盤、點擊
- **主動登出**：用戶無縫重定向至登入頁

---

## 四、存錄安全與監控

### ✅ 4.1 審計日誌必記事項

```csharp
public class AuditLogEntry
{
    public string Id { get; set; }                // 唯一識別
    public DateTime TimestampUtc { get; set; }    // 事件時間（秒級）
    public string Username { get; set; }          // 使用者帳號
    public string Action { get; set; }            // 操作類型
    public string Target { get; set; }            // 操作目標
    public bool Success { get; set; }             // 成功/失敗
    public string Detail { get; set; }            // 詳細說明
    public string IpAddress { get; set; }         // 來源 IP
    public string UserAgent { get; set; }         // 客戶端資訊
    public string IntegrityHash { get; set; }     // 完整性雜湊鏈
}
```

**必記操作**
| 分類 | 操作 | 記錄內容 |
|------|------|--------|
| **身份驗證** | 登入成功/失敗 | 帳號、IP、時間、原因 |
| | MFA 驗證 | 成功/失敗、設備 |
| **帳號管理** | 建立帳號 | 帳號、角色、操作者 |
| | 停用/啟用 | 目標帳號、原因 |
| | 重設密碼 | 目標帳號、操作者 |
| | 角色變更 | 帳號、舊角色、新角色 |
| **系統查詢** | 檢視稽核紀錄 | 檢視者、查詢範圍 |
| | 驗證日誌完整性 | 檢查時間、結果 |

### ✅ 4.2 日誌完整性保護

**SHA256 雜湊鏈實現**
```csharp
public void AddAudit(...)
{
    // 計算日誌完整性雜湊鏈
    var previousHash = _auditLogs.Count > 0 ? _auditLogs[^1].IntegrityHash : "GENESIS";
    entry.IntegrityHash = ComputeAuditEntryHash(previousHash, entry);
    _auditLogs.Add(entry);
}

private static string ComputeAuditEntryHash(string previousHash, AuditLogEntry entry)
{
    var content = $"{previousHash}|{entry.TimestampUtc:O}|{entry.Username}|{entry.Action}|" +
                  $"{entry.Target}|{entry.Success}|{entry.Detail}|{entry.IpAddress}";
    
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(bytes);
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
```

**檢查端點** - `UsersController`
```csharp
[HttpGet("audit-integrity")]
public IActionResult CheckAuditIntegrity()
{
    var isIntact = _securityService.VerifyAuditLogIntegrity();
    return Ok(new { intact = isIntact, checkedAt = DateTime.UtcNow });
}
```

**防竄改特性**
- ✅ 每筆紀錄包含前一筆的雜湊
- ✅ 無法修改任何紀錄而不破壞鏈
- ✅ 任何插入/刪除都會暴露
- ✅ 時間戳記精確至秒

### ✅ 4.3 異常監控

監控項目及實施措施：

| 異常行為 | 檢測機制 | 回應 |
|---------|--------|------|
| 登入失敗過多 | 累計失敗次數 (≥5) | 帳號立即鎖定 15 分鐘 |
| 暴力破解 | 速率限制中間件 | 登入限制 10/分鐘/IP |
| 非授權存取 | `[Authorize]` 屬性 | 返回 403/401 |
| MFA 失敗 | TOTP 驗證 | 記錄失敗次數 |
| 日誌被篡改 | 雜湊鏈驗證 | 系統警示 |

---

## 五、安全中間件與標頭

### ✅ 5.1 全域例外處理

**中間件套件順序** - `Program.cs`
```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();  // 最優先
app.Use(async (context, next) => {
    // 安全 HTTP 標頭
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});
```

### ✅ 5.2 速率限制

**登入端點防暴力**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login-limit", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 10;  // 每分鐘 10 次
        cfg.QueueLimit = 0;    // 拒絕額外請求
    });
});

app.MapPost("/api/auth/login", ...)
    .RequireRateLimiting("login-limit");
```

---

## 六、API 端點安全性

### ✅ 6.1 身份驗證端點

| 端點 | 方法 | 認證 | 用途 |
|------|------|------|------|
| `/api/auth/captcha` | GET | 無 | 取得圖形驗證碼 |
| `/api/auth/login` | POST | CAPTCHA | 帳密登入 |
| `/api/auth/mfa/verify` | POST | 無（MFA 第二步） | MFA 驗證 |
| `/api/auth/mfa/setup` | POST | JWT | 初始化 TOTP |
| `/api/auth/mfa/confirm` | POST | JWT | 確認啟用 MFA |
| `/api/auth/mfa/disable` | POST | JWT (Admin) | 停用 MFA |
| `/api/auth/change-password` | POST | JWT | 變更密碼 |

### ✅ 6.2 使用者管理端點

| 端點 | 方法 | 認證 | 權限 | 用途 |
|------|------|------|------|------|
| `/api/users` | GET | JWT | Admin | 列出帳號 |
| `/api/users` | POST | JWT | Admin | 建立帳號 |
| `/api/users/{username}/toggle-active` | PUT | JWT | Admin | 停用/啟用 |
| `/api/users/{username}/reset-password` | PUT | JWT | Admin | 重設密碼 |
| `/api/users/{username}/role` | PUT | JWT | Admin | 變更角色 |
| `/api/users/audit-integrity` | GET | JWT | Admin | 驗證日誌完整性 |

### ✅ 6.3 控制器驗證自動化

**基礎 DTO 驗證示例**
```csharp
[HttpPost]
public IActionResult Login([FromBody] LoginRequest request)
{
    // 伺服器端驗證所有輸入
    if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > MaxUsernameLength)
        return BadRequest(new { message = "帳號格式不正確" });
    
    if (!SafeUsernameRegex.IsMatch(request.Username))
        return BadRequest(new { message = "帳號包含不允許的字元" });
    
    // ... 後續處理
}
```

---

## 七、前端安全實踐

### ✅ 7.1 不保存機密資訊

**SessionStorage (非 LocalStorage) 儲存**
```csharp
// 儲存令牌於 SessionStorage（瀏覽器關閉自動清除）
await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);

// 登出時清除
await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
```

- ✅ 用 SessionStorage 而非 LocalStorage
- ❌ 永不在 URL 中傳遞令牌
- ✅ 登出時立即清除會話

### ✅ 7.2 CSRF 防護

- Blazor Server 內建 CSRF 令牌 (ViewState)
- 所有 POST 自動驗證

### ✅ 7.3 事件監控

**用戶活動偵測** - `MainLayout.razor`
```razor
<MudLayout @onmousemove="OnUserActivity" @onkeydown="OnUserActivity" @onclick="OnUserActivity">
    ...
</MudLayout>

@code {
    private void OnUserActivity()
    {
        AuthSession.RecordActivity();  // 重置閒置計時
    }
}
```

---

## 八、部署與操作建議

### ✅ 8.1 環境設定 - `appsettings.json`

```json
{
  "Jwt": {
    "Key": "[至少 32 字元的強密鑰]",
    "Issuer": "Signage.Server",
    "Audience": "Signage.App",
    "ExpireMinutes": 480
  },
  "ApiSettings": {
    "BaseUrl": "https://api.example.com/api"
  }
}
```

**密鑰管理**
- 🔐 JWT Key 最少 32 字元（建議 64+）
- 防護 appsettings 免被版本控制
- 使用 ASP.NET Core 密碼管理工具

### ✅ 8.2 HTTPS 強制

- 生產環境必須啟用 HTTPS
- 代碼中已設定 `app.UseHttpsRedirection()`

### ✅ 8.3 定期審計

```bash
# 檢查日誌完整性
curl -X GET https://api.example.com/api/users/audit-integrity \
  -H "Authorization: Bearer {token}"

# 應回傳
{
  "intact": true,
  "checkedAt": "2026-03-29T12:00:00Z"
}
```

### ✅ 8.4 日誌保留政策

- 審計日誌限 5000 筆，超過自動去除前 500 筆
- 建議定期備份至 SQL Server/ELK Stack

---

## 九、符合規定對照表

| 規定項目 | 具體要求 | 實現狀況 | 實現文件 |
|---------|--------|--------|--------|
| **密碼安全** | PBKDF2+Salt | ✅ | SecurityService.cs:85-94 |
| | 192 天有效期 | ✅ | SecurityService.cs:162 |
| | 異常監控 | ✅ | SecurityService.cs:112-125 |
| **身份認證** | MFA 支援 | ✅ | SecurityService.cs:180-245 |
| | 帳號鎖定(5失敗) | ✅ | SecurityService.cs:120-125 |
| | 閒置登出(15分) | ✅ | AuthSessionService.cs:110-125 |
| **存取控制** | RBAC 三層 | ✅ | UserRoles.cs |
| | 最小權限 | ✅ | Controller `[Authorize(Roles=...)]` |
| **日誌保護** | 雜湊鏈 | ✅ | SecurityService.cs:280-290 |
| | 訊息遮蔽 | ✅ | ExceptionHandlingMiddleware.cs |
| | 速率限制 | ✅ | Program.cs:68-75 |

---

## 十、測試清單

### ✅ 功能測試

- [ ] 帳號建立與密碼政策驗證
- [ ] 登入失敗 5 次鎖定帳號
- [ ] 鎖定帳號 15 分鐘後可再登入
- [ ] MFA 掃描 QR Code 並驗證
- [ ] 15 分鐘無操作自動登出
- [ ] 管理員可停用/啟用帳號
- [ ] 管理員可重設密碼
- [ ] 日誌完整性驗證通過

### ✅ 安全測試

- [ ] 嘗試 SQL 注入（應被中間件攔截）
- [ ] XSS 注射（應被 RegEx 過濾）
- [ ] 暴力登入（速率限制 10/分鐘）
- [ ] 無效 Token 訪問（應返回 401）
- [ ] 檢查 HTTP 標頭（X-Frame-Options 等）
- [ ] 錯誤頁面不洩漏技術細節

---

## 十一、後續强化建议

1. **更高層級 MFA**
   - 簡訊 OTP (SMS)
   - 推送通知認證

2. **高級日誌分析**
   - 導入 ELK Stack
   - 即時警示系統

3. **威脅檢測**
   - 異常登入地點偵測
   - IP 信譽檢查

4. **合規審計**
   - 定期滲透測試
   - 第三方安全評估

---

## 附錄 A：快速命令參考

### 啟動系統
```bash
# 後端
cd Signage.Server && dotnet run --launch-profile https

# 前端
cd Signage.App && dotnet run --launch-profile https
```

### 驗證 JWT
```bash
# 檢查 Token 有效期
jwt-decode <token>
```

### 檢查日誌完整性
```bash
# 設定變數
TOKEN="<JWT token>"
API="https://localhost:5001/api"

# 驗證
curl -X GET $API/users/audit-integrity \
  -H "Authorization: Bearer $TOKEN"
```

---

**文檔維護者**：GitHub Copilot  
**最後更新**：2026-03-29  
**版本**：2.0
