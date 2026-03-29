using Signage.Common;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace Signage.App.Services;

/// <summary>
/// 電子看板 API 客戶端服務
/// 用於與後端伺服器進行通信
/// </summary>
public class SignageApiClient
{
    private readonly HttpClient _httpClient;
    private string _apiBaseUrl = "";
    private readonly AuthSessionService _authSessionService;

    public SignageApiClient(HttpClient httpClient, IConfiguration configuration, AuthSessionService authSessionService)
    {
        _httpClient = httpClient;
        _apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001/api";
        _authSessionService = authSessionService;
    }

    private void AttachAuthHeader()
    {
        if (_authSessionService.IsAuthenticated)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authSessionService.Token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    // ============= 驗證與權限 API =============

    public async Task<CaptchaResponse?> GetCaptchaAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CaptchaResponse>($"{_apiBaseUrl}/auth/captcha");
        }
        catch
        {
            return null;
        }
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password, string captchaId, string captchaCode)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/auth/login", new
            {
                username,
                password,
                captchaId,
                captchaCode
            });

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Message = await ReadErrorMessageAsync(response, "登入失敗，請確認帳密與驗證碼")
                };
            }

            return await response.Content.ReadFromJsonAsync<LoginResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool success, string message)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/auth/change-password", new
            {
                currentPassword,
                newPassword
            });

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }

            return (true, "密碼更新成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<List<UserAccount>> GetUsersAsync()
    {
        try
        {
            AttachAuthHeader();
            return await _httpClient.GetFromJsonAsync<List<UserAccount>>($"{_apiBaseUrl}/users") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<(bool success, string message)> CreateUserAsync(string username, string displayName, string role, string password)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/users", new
            {
                username,
                displayName,
                role,
                password
            });

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }

            return (true, "帳號建立成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> ToggleUserActiveAsync(string username)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PutAsync($"{_apiBaseUrl}/users/{Uri.EscapeDataString(username)}/toggle-active", null);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }
            return (true, "狀態已更新");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> AdminResetPasswordAsync(string username, string newPassword)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/users/{Uri.EscapeDataString(username)}/reset-password", new { newPassword });
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }
            return (true, "密碼重設成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> UpdateUserRoleAsync(string username, string role)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/users/{Uri.EscapeDataString(username)}/role", new { role });
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }
            return (true, "角色更新成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? otpUri, string? secret, string message)> SetupMfaAsync()
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/auth/mfa/setup", null);
            if (!response.IsSuccessStatusCode)
                return (false, null, null, "MFA 初始化失敗");

            var data = await response.Content.ReadFromJsonAsync<MfaSetupResponse>();
            return (true, data?.OtpUri, data?.Secret, data?.Message ?? "");
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    public async Task<(bool success, string message)> ConfirmMfaAsync(string totpCode)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/auth/mfa/confirm", new { totpCode });
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }
            return (true, "MFA 啟用成功");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> DisableMfaAsync(string username)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/auth/mfa/disable", new { username });
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return (false, err);
            }
            return (true, "MFA 已停用");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<LoginResponse?> VerifyMfaAsync(string userId, string totpCode)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/auth/mfa/verify", new { userId, totpCode });
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<LoginResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<AuditLogEntry>> GetAuditLogsAsync(AuditLogQuery? query = null)
    {
        try
        {
            AttachAuthHeader();
            var path = $"{_apiBaseUrl}/auditlogs{BuildAuditLogQueryString(query)}";
            return await _httpClient.GetFromJsonAsync<List<AuditLogEntry>>(path) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<(bool success, string fileName, string base64Content, string message)> ExportAuditLogsCsvAsync(AuditLogQuery? query = null)
    {
        try
        {
            AttachAuthHeader();
            var path = $"{_apiBaseUrl}/auditlogs/export{BuildAuditLogQueryString(query)}";
            var response = await _httpClient.GetAsync(path);

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Empty, string.Empty, await ReadErrorMessageAsync(response, "匯出 CSV 失敗"));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentDisposition = response.Content.Headers.ContentDisposition;
            var fileName = contentDisposition?.FileNameStar
                           ?? contentDisposition?.FileName?.Trim('"')
                           ?? $"audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            return (true, fileName, Convert.ToBase64String(bytes), string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, string.Empty, ex.Message);
        }
    }

    // ============= 遊客中心 API =============

    public async Task<List<VisitorCenter>> GetAllVisitorCentersAsync()
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/visitorcenters");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<VisitorCenter>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching visitor centers: {ex.Message}");
            return new();
        }
    }

    public async Task<VisitorCenter?> GetVisitorCenterAsync(string centerId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/visitorcenters/{centerId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<VisitorCenter>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching visitor center: {ex.Message}");
            return null;
        }
    }

    public async Task<VisitorCenter?> CreateVisitorCenterAsync(string name, string location)
    {
        try
        {
            AttachAuthHeader();
            var request = new { name, location };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/visitorcenters", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<VisitorCenter>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating visitor center: {ex.Message}");
            return null;
        }
    }

    // ============= 版型配置 API =============

    public async Task<List<SignagePage>> GetAllSignagePagesAsync()
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/signagepages");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<SignagePage>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching all signage pages: {ex.Message}");
            return new();
        }
    }

    public async Task<List<SignagePage>> GetPagesByCenterAsync(string centerId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/signagepages/center/{centerId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<SignagePage>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching signage pages: {ex.Message}");
            return new();
        }
    }

    public async Task<SignagePage?> GetSignagePageAsync(string pageId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/signagepages/{pageId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching signage page: {ex.Message}");
            return null;
        }
    }

    public async Task<SignagePage?> GetActivePageByCenterAsync(string centerId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/signagepages/center/{centerId}/active");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching active signage page: {ex.Message}");
            return null;
        }
    }

    public async Task<SignagePage?> CreateSignagePageAsync(string centerId, string name, LayoutType layout)
    {
        try
        {
            AttachAuthHeader();
            var request = new { centerId, name, layout };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/signagepages", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating signage page: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateSignagePageAsync(string pageId, SignagePage page)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/signagepages/{pageId}", page);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating signage page: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ActivatePageAsync(string centerId, string pageId)
    {
        try
        {
            AttachAuthHeader();
            var request = new { centerId };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/signagepages/{pageId}/activate", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error activating page: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteSignagePageAsync(string pageId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/signagepages/{pageId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting signage page: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 根據設備 ID 取得該設備的播放版型
    /// </summary>
    public async Task<SignagePage?> GetPageByDeviceAsync(string deviceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/{deviceId}/play");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching page by device: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 根據中心 ID 和設備編號取得該設備的播放版型
    /// </summary>
    public async Task<SignagePage?> GetPageByDeviceNumberAsync(string centerId, int deviceNumber)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/center/{centerId}/device/{deviceNumber}/play");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching page by device number: {ex.Message}");
            return null;
        }
    }

    public async Task<SignagePage?> GetPageByDeviceSlugAsync(string deviceSlug)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/play/{deviceSlug}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SignagePage>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching page by slug: {ex.Message}");
            return null;
        }
    }

    public async Task<List<DeviceRegionContent>> GetDeviceRegionContentsAsync(string centerId, int deviceNumber)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/center/{centerId}/device/{deviceNumber}/content");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DeviceRegionContent>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching device contents: {ex.Message}");
            return new();
        }
    }

    public async Task<bool> UpdateDeviceRegionContentsAsync(string centerId, int deviceNumber, List<DeviceRegionContent> regionContents)
    {
        try
        {
            AttachAuthHeader();
            var request = new { regionContents };
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/devices/center/{centerId}/device/{deviceNumber}/content", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating device contents: {ex.Message}");
            return false;
        }
    }

    // ============= 媒體 API =============

    public async Task<List<MediaContent>> GetAllMediaAsync()
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/media");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MediaContent>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching media: {ex.Message}");
            return new();
        }
    }

    public async Task<MediaContent?> AddMediaAsync(string title, string url, string mediaType, int duration)
    {
        try
        {
            AttachAuthHeader();
            var request = new { title, url, mediaType, duration };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/media", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MediaContent>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding media: {ex.Message}");
            return null;
        }
    }

    public async Task<List<MediaContent>> GetMediaByCenterAsync(string centerId)
    {
        return await GetAllMediaAsync();
    }

    public async Task<bool> DeleteMediaAsync(string mediaId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/media/{mediaId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error deleting media: {ex.Message}");
            return false;
        }
    }

    // ============= 設備 API =============

    public async Task<List<DeviceStatus>> GetDevicesByCenterAsync(string centerId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/center/{centerId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DeviceStatus>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching devices: {ex.Message}");
            return new();
        }
    }

    public async Task<List<DeviceHierarchyNode>> GetDeviceHierarchyByCenterAsync(string centerId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/devices/center/{centerId}/hierarchy");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<DeviceHierarchyNode>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching device hierarchy: {ex.Message}");
            return new();
        }
    }

    public async Task<DeviceStatus?> RegisterDeviceAsync(string centerId, string deviceName, string ipAddress)
    {
        try
        {
            AttachAuthHeader();
            var request = new { centerId, deviceName, ipAddress };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/devices/register", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DeviceStatus>();
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error registering device: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateDeviceHeartbeatAsync(string deviceId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/devices/{deviceId}/heartbeat", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating device heartbeat: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateDeviceConfigAsync(string centerId, int deviceNumber, string deviceName, string assignedPageId, ScreenOrientation screenOrientation, bool isActive, string deviceSlug)
    {
        try
        {
            AttachAuthHeader();
            var request = new
            {
                deviceName,
                assignedPageId,
                screenOrientation,
                isActive,
                deviceSlug
            };
            var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/devices/center/{centerId}/device/{deviceNumber}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating device config: {ex.Message}");
            return false;
        }
    }

    // ============= 推送命令 API =============

    public async Task<SignagePushCommand?> PushConfigurationAsync(string deviceId, SignagePage pageConfig)
    {
        try
        {
            AttachAuthHeader();
            var request = new { deviceId, pageConfig };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/commands/push", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (result.TryGetProperty("command", out var commandEl))
                    return System.Text.Json.JsonSerializer.Deserialize<SignagePushCommand>(commandEl.GetRawText());
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error pushing configuration: {ex.Message}");
            return null;
        }
    }

    public async Task<List<SignagePushCommand>> GetPendingCommandsAsync(string deviceId)
    {
        try
        {
            AttachAuthHeader();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/commands/pending/{deviceId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<SignagePushCommand>>() ?? new();
            }
            return new();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fetching pending commands: {ex.Message}");
            return new();
        }
    }

    public async Task<bool> UpdateCommandStatusAsync(string commandId, string status)
    {
        try
        {
            AttachAuthHeader();
            var request = new { status };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/commands/{commandId}/status", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating command status: {ex.Message}");
            return false;
        }
    }

    private static string BuildAuditLogQueryString(AuditLogQuery? query)
    {
        var effective = query ?? new AuditLogQuery();
        var parts = new List<string>();

        if (effective.Limit > 0)
            parts.Add($"limit={effective.Limit}");

        if (effective.FromUtc.HasValue)
            parts.Add($"fromUtc={Uri.EscapeDataString(effective.FromUtc.Value.ToUniversalTime().ToString("O"))}");

        if (effective.ToUtc.HasValue)
            parts.Add($"toUtc={Uri.EscapeDataString(effective.ToUtc.Value.ToUniversalTime().ToString("O"))}");

        if (!string.IsNullOrWhiteSpace(effective.Username))
            parts.Add($"username={Uri.EscapeDataString(effective.Username.Trim())}");

        if (!string.IsNullOrWhiteSpace(effective.Action))
            parts.Add($"action={Uri.EscapeDataString(effective.Action.Trim())}");

        if (effective.Success.HasValue)
            parts.Add($"success={effective.Success.Value.ToString().ToLowerInvariant()}");

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, string fallbackMessage)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return string.IsNullOrWhiteSpace(payload?.Message) ? fallbackMessage : payload.Message;
        }
        catch
        {
            return fallbackMessage;
        }
    }
}

public class CaptchaResponse
{
    public string CaptchaId { get; set; } = "";
    public string ImageBase64 { get; set; } = "";
    public string Format { get; set; } = "image/svg+xml";
}

public class LoginResponse
{
    public string Message { get; set; } = "";
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool RequiresPasswordChange { get; set; }
    public DateTime PasswordExpiresAtUtc { get; set; }
    // MFA 兩步驟回應
    public bool RequiresMfa { get; set; }
    public string UserId { get; set; } = "";
}

public class MfaSetupResponse
{
    public string Message { get; set; } = "";
    public string OtpUri { get; set; } = "";
    public string Secret { get; set; } = "";
}

public class ApiMessageResponse
{
    public string Message { get; set; } = "";
}
