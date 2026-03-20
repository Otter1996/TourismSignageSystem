using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Signage.Server.Services;

namespace Signage.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly SecurityService _securityService;

    public AuditLogsController(SecurityService securityService)
    {
        _securityService = securityService;
    }

    [HttpGet]
    public IActionResult GetAuditLogs([FromQuery] int limit = 200)
    {
        return Ok(_securityService.GetAuditLogs(limit));
    }
}
