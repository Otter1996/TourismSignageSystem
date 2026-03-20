using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Signage.Server.Controllers;

/// <summary>
/// 版型配置管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SignagePagesController : ControllerBase
{
    private readonly SignageDataService _dataService;
    private readonly SecurityService _securityService;

    public SignagePagesController(SignageDataService dataService, SecurityService securityService)
    {
        _dataService = dataService;
        _securityService = securityService;
    }

    /// <summary>
    /// 取得指定中心的所有版型配置
    /// </summary>
    [HttpGet("center/{centerId}")]
    public ActionResult<List<SignagePage>> GetPagesByCenter(string centerId)
    {
        var pages = _dataService.GetPagesByCenter(centerId);
        return Ok(pages);
    }

    /// <summary>
    /// 取得單一版型配置
    /// </summary>
    [HttpGet("{pageId}")]
    public ActionResult<SignagePage> GetById(string pageId)
    {
        var page = _dataService.GetSignagePage(pageId);
        if (page == null)
            return NotFound(new { message = "版型配置不存在" });
        return Ok(page);
    }

    /// <summary>
    /// 建立新版型配置
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult<SignagePage> Create([FromBody] CreateSignagePageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CenterId))
            return BadRequest(new { message = "中心 ID 不能為空" });

        var page = _dataService.CreateSignagePage(request.CenterId, request.Name ?? "預設版型", request.Layout);
        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "SignagePage.Create", page.Id, true, $"建立版型 {page.Name}", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return CreatedAtAction("GetById", new { pageId = page.Id }, page);
    }

    /// <summary>
    /// 更新版型配置
    /// </summary>
    [HttpPut("{pageId}")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult UpdateSignagePage(string pageId, [FromBody] SignagePage updatedPage)
    {
        var success = _dataService.UpdateSignagePage(pageId, updatedPage);
        if (!success)
            return NotFound(new { message = "版型配置不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "SignagePage.Update", pageId, true, "更新版型配置", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "更新成功", page = _dataService.GetSignagePage(pageId) });
    }

    /// <summary>
    /// 啟用版型配置（推送至所有設備）
    /// </summary>
    [HttpPost("{pageId}/activate")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult ActivatePage(string pageId, [FromBody] ActivatePageRequest request)
    {
        var page = _dataService.GetSignagePage(pageId);
        if (page == null)
            return NotFound(new { message = "版型配置不存在" });

        var success = _dataService.ActivatePage(page.VisitorCenterId, pageId);
        if (!success)
            return BadRequest(new { message = "啟用失敗" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "SignagePage.Activate", pageId, true, $"啟用版型 {page.Name}", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());

        return Ok(new { message = $"版型 {page.Name} 已啟用", pageId = pageId });
    }
}

/// <summary>
/// 建立版型配置請求 DTO
/// </summary>
public class CreateSignagePageRequest
{
    public string CenterId { get; set; } = "";
    public string? Name { get; set; }
    public LayoutType Layout { get; set; } = LayoutType.TripleVertical;
}

/// <summary>
/// 啟用版型配置請求 DTO
/// </summary>
public class ActivatePageRequest
{
    public string CenterId { get; set; } = "";
}
