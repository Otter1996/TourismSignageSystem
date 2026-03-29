using System.Text.Json;
using Microsoft.JSInterop;

namespace Signage.App.Services;

public class AuthSessionService : IDisposable
{
    private const string StorageKey = "signage.auth.session";
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

    private readonly IJSRuntime _jsRuntime;
    private System.Threading.Timer? _idleTimer;
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    public event Action? OnIdleLogout;
    public event Action? OnSessionChanged;

    public AuthSessionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
    public bool IsInitialized { get; private set; }
    public string Token { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string Role { get; private set; } = "";
    public bool RequiresPasswordChange { get; private set; }

    public async Task InitializeAsync()
    {
        if (IsInitialized)
            return;

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var snapshot = JsonSerializer.Deserialize<AuthSessionSnapshot>(json);
                if (snapshot is not null)
                {
                    ApplySnapshot(snapshot);
                }
            }
        }
        catch
        {
            ClearSession();
        }
        finally
        {
            IsInitialized = true;
        }
    }

    public async Task SetSessionAsync(string token, string username, string displayName, string role, bool requiresPasswordChange)
    {
        ApplySnapshot(new AuthSessionSnapshot
        {
            Token = token,
            Username = username,
            DisplayName = displayName,
            Role = role,
            RequiresPasswordChange = requiresPasswordChange
        });

        IsInitialized = true;
        _lastActivityUtc = DateTime.UtcNow;
        StartIdleTimer();
        await PersistAsync();
    }

    /// <summary>
    /// 使用者有任何互動時呼叫此方法以重置閒置計時
    /// </summary>
    public void RecordActivity()
    {
        if (!IsAuthenticated) return;
        _lastActivityUtc = DateTime.UtcNow;
    }

    public async Task LogoutAsync()
    {
        StopIdleTimer();
        ClearSession();
        IsInitialized = true;

        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
        }
        catch
        {
            // Ignore browser storage failures during logout.
        }
        OnSessionChanged?.Invoke();
    }

    private void StartIdleTimer()
    {
        StopIdleTimer();
        // 每分鐘檢查一次閒置狀態
        _idleTimer = new System.Threading.Timer(CheckIdle, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void StopIdleTimer()
    {
        _idleTimer?.Dispose();
        _idleTimer = null;
    }

    private void CheckIdle(object? _)
    {
        if (!IsAuthenticated) return;

        if (DateTime.UtcNow - _lastActivityUtc >= IdleTimeout)
        {
            StopIdleTimer();
            ClearSession();
            // 清除 sessionStorage（注意：Timer callback 在非 Blazor 執行緒，透過事件通知 UI 執行）
            OnIdleLogout?.Invoke();
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(new AuthSessionSnapshot
        {
            Token = Token,
            Username = Username,
            DisplayName = DisplayName,
            Role = Role,
            RequiresPasswordChange = RequiresPasswordChange
        });

        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
    }

    private void ApplySnapshot(AuthSessionSnapshot snapshot)
    {
        Token = snapshot.Token;
        Username = snapshot.Username;
        DisplayName = snapshot.DisplayName;
        Role = snapshot.Role;
        RequiresPasswordChange = snapshot.RequiresPasswordChange;
    }

    private void ClearSession()
    {
        Token = "";
        Username = "";
        DisplayName = "";
        Role = "";
        RequiresPasswordChange = false;
    }

    public void Dispose()
    {
        StopIdleTimer();
    }

    private sealed class AuthSessionSnapshot
    {
        public string Token { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool RequiresPasswordChange { get; set; }
    }
}
