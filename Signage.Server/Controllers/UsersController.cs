using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Signage.Common;
using Signage.Server.Services;

namespace Signage.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private const int MaxUsernameLength = 64;
    private const int MaxDisplayNameLength = 100;
    private const int MaxPasswordLength = 128;
    private static readonly Regex SafeUsernameRegex = new(@"^[a-zA-Z0-9_\-\.@]{1,64}$", RegexOptions.Compiled);

    private readonly SecurityService _securityService;

    public UsersController(SecurityService securityService)
    {
        _securityService = securityService;
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        return Ok(_securityService.GetUsers());
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > MaxUsernameLength)
            return BadRequest(new { message = "帳號格式不正確" });

        if (!SafeUsernameRegex.IsMatch(request.Username))
            return BadRequest(new { message = "帳號包含不允許的字元" });

        if ((request.DisplayName?.Length ?? 0) > MaxDisplayNameLength)
            return BadRequest(new { message = "顯示名稱過長" });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > MaxPasswordLength)
            return BadRequest(new { message = "密碼格式不正確" });

        if (!UserRoles.All.Contains(request.Role))
            return BadRequest(new { message = "角色不合法" });

        var actor = User.Identity?.Name ?? "unknown";
        var result = _securityService.CreateUser(request.Username, request.DisplayName, request.Role, request.Password, actor);
        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message, user = result.user });
    }

    [HttpPut("{username}/toggle-active")]
    public IActionResult ToggleActive(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
            return BadRequest(new { message = "帳號格式不正確" });

        var actor = User.Identity?.Name ?? "unknown";
        var result = _securityService.ToggleUserActive(username, actor);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    [HttpPut("{username}/reset-password")]
    public IActionResult ResetPassword(string username, [FromBody] AdminResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
            return BadRequest(new { message = "帳號格式不正確" });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > MaxPasswordLength)
            return BadRequest(new { message = "密碼格式不正確" });

        var actor = User.Identity?.Name ?? "unknown";
        var result = _securityService.AdminResetPassword(username, request.NewPassword, actor);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    [HttpPut("{username}/role")]
    public IActionResult UpdateRole(string username, [FromBody] UpdateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
            return BadRequest(new { message = "帳號格式不正確" });

        if (!UserRoles.All.Contains(request.Role))
            return BadRequest(new { message = "角色不合法" });

        var actor = User.Identity?.Name ?? "unknown";
        var result = _securityService.UpdateUserRole(username, request.Role, actor);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    [HttpGet("audit-integrity")]
    public IActionResult CheckAuditIntegrity()
    {
        var isIntact = _securityService.VerifyAuditLogIntegrity();
        return Ok(new { intact = isIntact, checkedAt = DateTime.UtcNow });
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Viewer";
    public string Password { get; set; } = "";
}

public class AdminResetPasswordRequest
{
    public string NewPassword { get; set; } = "";
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = "";
}
