using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Signage.Server.Controllers;

/// <summary>
/// 遊客中心管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisitorCentersController : ControllerBase
{
    private readonly SignageDataService _dataService;
    private readonly SecurityService _securityService;

    public VisitorCentersController(SignageDataService dataService, SecurityService securityService)
    {
        _dataService = dataService;
        _securityService = securityService;
    }

    /// <summary>
    /// 取得所有遊客中心
    /// </summary>
    [HttpGet]
    public ActionResult<List<VisitorCenter>> GetAll()
    {
        return Ok(_dataService.GetAllVisitorCenters());
    }

    /// <summary>
    /// 取得單一遊客中心
    /// </summary>
    [HttpGet("{centerId}")]
    public ActionResult<VisitorCenter> GetById(string centerId)
    {
        var center = _dataService.GetVisitorCenter(centerId);
        if (center == null)
            return NotFound(new { message = "遊客中心不存在" });
        return Ok(center);
    }

    /// <summary>
    /// 建立新遊客中心
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult<VisitorCenter> Create([FromBody] CreateVisitorCenterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "中心名稱不能為空" });

        var center = _dataService.CreateVisitorCenter(request.Name, request.Location ?? "");
        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "VisitorCenter.Create", center.Id, true, $"建立中心 {center.Name}", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return CreatedAtAction("GetById", new { centerId = center.Id }, center);
    }

    /// <summary>
    /// 更新遊客中心
    /// </summary>
    [HttpPut("{centerId}")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult UpdateVisitorCenter(string centerId, [FromBody] UpdateVisitorCenterRequest request)
    {
        var success = _dataService.UpdateVisitorCenter(centerId, request.Name, request.Location, request.Description ?? "");
        if (!success)
            return NotFound(new { message = "遊客中心不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "VisitorCenter.Update", centerId, true, "更新遊客中心", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "更新成功" });
    }

    /// <summary>
    /// 刪除遊客中心
    /// </summary>
    [HttpDelete("{centerId}")]
    [Authorize(Roles = "Admin")]
    public ActionResult DeleteVisitorCenter(string centerId)
    {
        var success = _dataService.DeleteVisitorCenter(centerId);
        if (!success)
            return NotFound(new { message = "遊客中心不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "VisitorCenter.Delete", centerId, true, "刪除遊客中心", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "刪除成功" });
    }
}

/// <summary>
/// 建立遊客中心請求 DTO
/// </summary>
public class CreateVisitorCenterRequest
{
    public string Name { get; set; } = "";
    public string? Location { get; set; }
}

/// <summary>
/// 更新遊客中心請求 DTO
/// </summary>
public class UpdateVisitorCenterRequest
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string? Description { get; set; }
}
