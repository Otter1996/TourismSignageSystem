using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Signage.Server.Services;

namespace Signage.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
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
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { message = "帳號不可為空" });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "密碼不可為空" });

        var actor = User.Identity?.Name ?? "unknown";
        var result = _securityService.CreateUser(request.Username, request.DisplayName, request.Role, request.Password, actor);
        if (!result.success)
            return BadRequest(new { message = result.message });

        return Ok(new { message = result.message, user = result.user });
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Viewer";
    public string Password { get; set; } = "";
}
