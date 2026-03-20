namespace Signage.App.Services;

public class AuthSessionService
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
    public string Token { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string Role { get; private set; } = "";
    public bool RequiresPasswordChange { get; private set; }

    public void SetSession(string token, string username, string displayName, string role, bool requiresPasswordChange)
    {
        Token = token;
        Username = username;
        DisplayName = displayName;
        Role = role;
        RequiresPasswordChange = requiresPasswordChange;
    }

    public void Logout()
    {
        Token = "";
        Username = "";
        DisplayName = "";
        Role = "";
        RequiresPasswordChange = false;
    }
}
