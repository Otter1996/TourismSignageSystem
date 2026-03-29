using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
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
    private const int MaxUsernameLength = 64;
    private const int MaxPasswordLength = 128;
    private static readonly Regex SafeUsernameRegex = new(@"^[a-zA-Z0-9_\-\.@]{1,64}$", RegexOptions.Compiled);

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
        // 伺服器端輸入驗證
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > MaxUsernameLength)
            return BadRequest(new { message = "帳號格式不正確" });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > MaxPasswordLength)
            return BadRequest(new { message = "密碼格式不正確" });

        if (!SafeUsernameRegex.IsMatch(request.Username))
            return BadRequest(new { message = "帳號包含不允許的字元" });

        if (string.IsNullOrWhiteSpace(request.CaptchaId) || string.IsNullOrWhiteSpace(request.CaptchaCode))
            return BadRequest(new { message = "請填寫圖形驗證碼" });

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

        // MFA 流程：密碼驗證通過但需要第二因素
        if (result.RequiresMfa)
        {
            return Ok(new
            {
                requiresMfa = true,
                userId = result.User.Id,
                message = result.Message
            });
        }

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

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public IActionResult VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.TotpCode))
            return BadRequest(new { message = "參數不完整" });

        if (request.TotpCode.Length != 6 || !request.TotpCode.All(char.IsDigit))
            return BadRequest(new { message = "驗證碼格式不正確" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = _securityService.VerifyMfaCode(request.UserId, request.TotpCode, ip, userAgent);

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

    [HttpPost("mfa/setup")]
    [Authorize]
    public IActionResult SetupMfa()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "無效身分" });

        var result = _securityService.SetupMfa(username, username);
        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message, otpUri = result.qrCodeUri, secret = result.secret });
    }

    [HttpPost("mfa/confirm")]
    [Authorize]
    public IActionResult ConfirmMfa([FromBody] MfaConfirmRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "無效身分" });

        if (string.IsNullOrWhiteSpace(request.TotpCode) || request.TotpCode.Length != 6 || !request.TotpCode.All(char.IsDigit))
            return BadRequest(new { message = "驗證碼格式不正確" });

        var result = _securityService.ConfirmMfa(username, request.TotpCode, username);
        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    [HttpPost("mfa/disable")]
    [Authorize(Roles = "Admin")]
    public IActionResult DisableMfa([FromBody] MfaTargetRequest request)
    {
        var actor = User.Identity?.Name ?? "unknown";

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "帳號不可為空" });

        var result = _securityService.DisableMfa(request.Username, actor);
        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message });
    }

    [HttpPost("change-password")]
    [Authorize]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "無效身分" });

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "密碼不可為空" });

        if (request.NewPassword.Length > MaxPasswordLength)
            return BadRequest(new { message = "密碼長度超過上限" });

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

public class MfaVerifyRequest
{
    public string UserId { get; set; } = "";
    public string TotpCode { get; set; } = "";
}

public class MfaConfirmRequest
{
    public string TotpCode { get; set; } = "";
}

public class MfaTargetRequest
{
    public string Username { get; set; } = "";
}

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Signage.Server";
    public string Audience { get; set; } = "Signage.App";
    public int ExpireMinutes { get; set; } = 480;
}
