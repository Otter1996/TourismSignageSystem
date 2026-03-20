using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Signage.Common;
using Signage.Server.Services;

namespace Signage.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SecurityService _securityService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(SecurityService securityService, IOptions<JwtOptions> jwtOptions)
    {
        _securityService = securityService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpGet("captcha")]
    [AllowAnonymous]
    public IActionResult GetCaptcha()
    {
        var (captchaId, imageBase64) = _securityService.CreateCaptcha();
        return Ok(new { captchaId, imageBase64, format = "image/svg+xml" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = _securityService.Authenticate(
            request.Username,
            request.Password,
            request.CaptchaId,
            request.CaptchaCode,
            ip,
            userAgent);

        if (!result.Success || result.User is null)
            return Unauthorized(new { message = result.Message });

        var token = GenerateToken(result.User);

        return Ok(new
        {
            message = result.Message,
            token,
            username = result.User.Username,
            displayName = result.User.DisplayName,
            role = result.User.Role,
            requiresPasswordChange = result.RequiresPasswordChange,
            passwordExpiresAtUtc = result.User.PasswordChangedAtUtc.AddDays(180)
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "無效身分" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = _securityService.ChangePassword(username, request.CurrentPassword, request.NewPassword, ip, userAgent);

        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    private string GenerateToken(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string CaptchaId { get; set; } = "";
    public string CaptchaCode { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Signage.Server";
    public string Audience { get; set; } = "Signage.App";
    public int ExpireMinutes { get; set; } = 480;
}
