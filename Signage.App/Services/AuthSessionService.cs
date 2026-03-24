using System.Text.Json;
using Microsoft.JSInterop;

namespace Signage.App.Services;

public class AuthSessionService
{
    private const string StorageKey = "signage.auth.session";
    private readonly IJSRuntime _jsRuntime;

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
        await PersistAsync();
    }

    public async Task LogoutAsync()
    {
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

    private sealed class AuthSessionSnapshot
    {
        public string Token { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool RequiresPasswordChange { get; set; }
    }
}
