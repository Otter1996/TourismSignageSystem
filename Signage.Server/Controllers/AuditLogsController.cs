using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Signage.Common;
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
    public IActionResult GetAuditLogs([FromQuery] AuditLogQuery query)
    {
        return Ok(_securityService.GetAuditLogs(query));
    }

    [HttpGet("export")]
    public IActionResult ExportAuditLogsCsv([FromQuery] AuditLogQuery query)
    {
        var csv = _securityService.BuildAuditLogsCsv(query);
        var withBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return File(withBom, "text/csv; charset=utf-8", fileName);
    }
}
