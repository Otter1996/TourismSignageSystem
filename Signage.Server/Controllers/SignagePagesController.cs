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
    /// 取得全部版型配置（全域版型庫）
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<List<SignagePage>> GetAllPages()
    {
        return Ok(_dataService.GetAllSignagePages());
    }

    /// <summary>
    /// 取得指定中心的所有版型配置
    /// </summary>
    [HttpGet("center/{centerId}")]
    [AllowAnonymous]
    public ActionResult<List<SignagePage>> GetPagesByCenter(string centerId)
    {
        var pages = _dataService.GetPagesByCenter(centerId);
        return Ok(pages);
    }

    /// <summary>
    /// 取得指定中心目前啟用的版型配置
    /// </summary>
    [HttpGet("center/{centerId}/active")]
    [AllowAnonymous]
    public ActionResult<SignagePage> GetActivePageByCenter(string centerId)
    {
        var page = _dataService.GetActivePageByCenter(centerId);
        if (page == null)
            return NotFound(new { message = "目前無啟用版型" });

        return Ok(page);
    }

    /// <summary>
    /// 根據設備 ID 取得該設備的播放版型
    /// </summary>
    [HttpGet("device/{deviceId}")]
    [AllowAnonymous]
    public ActionResult<SignagePage> GetPageByDevice(string deviceId)
    {
        var page = _dataService.GetPageByDevice(deviceId);
        if (page == null)
            return NotFound(new { message = "設備未配置版型" });

        return Ok(page);
    }

    /// <summary>
    /// 根據設備公開代碼取得該設備的播放版型
    /// </summary>
    [HttpGet("play/{deviceSlug}")]
    [AllowAnonymous]
    public ActionResult<SignagePage> GetPageByDeviceSlug(string deviceSlug)
    {
        var page = _dataService.GetPageByDeviceSlug(deviceSlug);
        if (page == null)
            return NotFound(new { message = "設備未配置版型" });

        return Ok(page);
    }

    /// <summary>
    /// 根據中心 ID 和設備編號取得該設備的播放版型
    /// </summary>
    [HttpGet("center/{centerId}/device/{deviceNumber}")]
    [AllowAnonymous]
    public ActionResult<SignagePage> GetPageByDeviceNumber(string centerId, int deviceNumber)
    {
        var page = _dataService.GetPageByDeviceNumber(centerId, deviceNumber);
        if (page == null)
            return NotFound(new { message = "設備未配置或版型不存在" });

        return Ok(page);
    }

    /// <summary>
    /// 取得單一版型配置
    /// </summary>
    [HttpGet("{pageId}")]
    [AllowAnonymous]
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
        var centerId = request.CenterId ?? "";
        var page = _dataService.CreateSignagePage(centerId, request.Name ?? "預設版型", request.Layout);
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

    /// <summary>
    /// 刪除版型配置
    /// </summary>
    [HttpDelete("{pageId}")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult Delete(string pageId)
    {
        var success = _dataService.DeleteSignagePage(pageId);
        if (!success)
            return NotFound(new { message = "版型配置不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "SignagePage.Delete", pageId, true, "刪除版型配置", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "刪除成功" });
    }
}

/// <summary>
/// 建立版型配置請求 DTO
/// </summary>
public class CreateSignagePageRequest
{
    public string? CenterId { get; set; }
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
