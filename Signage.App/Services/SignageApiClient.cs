using Signage.Common;
using System.Net.Http.Json;

namespace Signage.App.Services;

/// <summary>
/// 電子看板 API 客戶端服務
/// 用於與後端伺服器進行通信
/// </summary>
public class SignageApiClient
{
    private readonly HttpClient _httpClient;
    private string _apiBaseUrl = "";

    public SignageApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001/api";
    }

    // ============= 遊客中心 API =============

    public async Task<List<VisitorCenter>> GetAllVisitorCentersAsync()
    {
        try
        {
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

    public async Task<List<SignagePage>> GetPagesByCenterAsync(string centerId)
    {
        try
        {
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

    public async Task<SignagePage?> CreateSignagePageAsync(string centerId, string name, LayoutType layout)
    {
        try
        {
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

    // ============= 媒體 API =============

    public async Task<List<MediaContent>> GetMediaByCenterAsync(string centerId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/media/center/{centerId}");
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

    public async Task<MediaContent?> AddMediaAsync(string centerId, string title, string url, string mediaType, int duration)
    {
        try
        {
            var request = new { centerId, title, url, mediaType, duration };
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

    // ============= 設備 API =============

    public async Task<List<DeviceStatus>> GetDevicesByCenterAsync(string centerId)
    {
        try
        {
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

    public async Task<DeviceStatus?> RegisterDeviceAsync(string centerId, string deviceName, string ipAddress)
    {
        try
        {
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
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/devices/{deviceId}/heartbeat", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error updating device heartbeat: {ex.Message}");
            return false;
        }
    }

    // ============= 推送命令 API =============

    public async Task<SignagePushCommand?> PushConfigurationAsync(string deviceId, SignagePage pageConfig)
    {
        try
        {
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
}
