using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Signage.Server.Controllers;

/// <summary>
/// 看板設備管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly SignageDataService _dataService;
    private readonly SecurityService _securityService;

    public DevicesController(SignageDataService dataService, SecurityService securityService)
    {
        _dataService = dataService;
        _securityService = securityService;
    }

    /// <summary>
    /// 取得所有設備
    /// </summary>
    [HttpGet]
    public ActionResult<List<DeviceStatus>> GetAll()
    {
        return Ok(_dataService.GetAllDevices());
    }

    /// <summary>
    /// 取得指定中心的所有設備
    /// </summary>
    [HttpGet("center/{centerId}")]
    public ActionResult<List<DeviceStatus>> GetDevicesByCenter(string centerId)
    {
        var devices = _dataService.GetDevicesByCenter(centerId);
        return Ok(devices);
    }

    /// <summary>
    /// 取得中心 > 設備 層級清單
    /// </summary>
    [HttpGet("center/{centerId}/hierarchy")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult<List<DeviceHierarchyNode>> GetHierarchyByCenter(string centerId)
    {
        return Ok(_dataService.GetDeviceHierarchyByCenter(centerId));
    }

    /// <summary>
    /// 更新設備設定（名稱、版型、方向、slug）
    /// </summary>
    [HttpPut("center/{centerId}/device/{deviceNumber}")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult UpdateDeviceConfig(string centerId, int deviceNumber, [FromBody] UpdateDeviceConfigRequest request)
    {
        var success = _dataService.UpdateDeviceConfig(
            centerId,
            deviceNumber,
            request.DeviceName,
            request.AssignedPageId,
            request.ScreenOrientation,
            request.IsActive,
            request.DeviceSlug);

        if (!success)
            return BadRequest(new { message = "設備設定更新失敗（可能是 slug 重複或設備不存在）" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "Device.UpdateConfig", $"{centerId}:{deviceNumber}", true, "更新設備設定", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "設備設定已更新" });
    }

    /// <summary>
    /// 註冊新設備
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult<DeviceStatus> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CenterId))
            return BadRequest(new { message = "中心 ID 不能為空" });

        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "設備名稱不能為空" });

        DeviceStatus device;
        try
        {
            device = _dataService.RegisterDevice(
                request.CenterId,
                request.DeviceName,
                request.IpAddress ?? ""
            );
        }
        catch (InvalidOperationException ex) when (ex.Message == "max-devices-reached")
        {
            return BadRequest(new { message = "每個中心最多只能設定 3 台設備" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "center-not-found")
        {
            return NotFound(new { message = "中心不存在" });
        }

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "Device.Register", device.DeviceId, true, $"註冊設備 {device.DeviceName}", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());

        return CreatedAtAction("GetDevicesByCenter", new { centerId = request.CenterId }, device);
    }

    /// <summary>
    /// 設備心跳（更新設備在線狀態）
    /// </summary>
    [HttpPost("{deviceId}/heartbeat")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult Heartbeat(string deviceId)
    {
        var success = _dataService.UpdateDeviceHeartbeat(deviceId);
        if (!success)
            return NotFound(new { message = "設備不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "Device.Heartbeat", deviceId, true, "設備心跳更新", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "心跳已更新", deviceId = deviceId });
    }
}

/// <summary>
/// 註冊設備請求 DTO
/// </summary>
public class RegisterDeviceRequest
{
    public string CenterId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? IpAddress { get; set; }
}

public class UpdateDeviceConfigRequest
{
    public string? DeviceName { get; set; }
    public string? AssignedPageId { get; set; }
    public ScreenOrientation? ScreenOrientation { get; set; }
    public bool? IsActive { get; set; }
    public string? DeviceSlug { get; set; }
}
