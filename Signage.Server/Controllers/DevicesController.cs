using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Signage.Server.Controllers;

/// <summary>
/// 看板設備管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly SignageDataService _dataService;

    public DevicesController(SignageDataService dataService)
    {
        _dataService = dataService;
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
    /// 註冊新設備
    /// </summary>
    [HttpPost("register")]
    public ActionResult<DeviceStatus> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CenterId))
            return BadRequest(new { message = "中心 ID 不能為空" });

        if (string.IsNullOrWhiteSpace(request.DeviceName))
            return BadRequest(new { message = "設備名稱不能為空" });

        var device = _dataService.RegisterDevice(
            request.CenterId,
            request.DeviceName,
            request.IpAddress ?? ""
        );

        return CreatedAtAction("GetDevicesByCenter", new { centerId = request.CenterId }, device);
    }

    /// <summary>
    /// 設備心跳（更新設備在線狀態）
    /// </summary>
    [HttpPost("{deviceId}/heartbeat")]
    public ActionResult Heartbeat(string deviceId)
    {
        var success = _dataService.UpdateDeviceHeartbeat(deviceId);
        if (!success)
            return NotFound(new { message = "設備不存在" });
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
