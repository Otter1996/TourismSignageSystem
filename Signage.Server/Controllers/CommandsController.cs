using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Signage.Server.Controllers;

/// <summary>
/// 推送命令管理 API（用於將版型配置推送至設備）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly SignageDataService _dataService;

    public CommandsController(SignageDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// 建立推送命令（推送版型配置至指定設備）
    /// </summary>
    [HttpPost("push")]
    public ActionResult<SignagePushCommand> PushConfiguration([FromBody] PushConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "設備 ID 不能為空" });

        if (request.PageConfig == null)
            return BadRequest(new { message = "版型配置不能為空" });

        var command = _dataService.CreatePushCommand(request.DeviceId, request.PageConfig);
        return Ok(new
        {
            message = "推送命令已建立，等待設備確認",
            command = command
        });
    }

    /// <summary>
    /// 取得設備的待推送命令
    /// </summary>
    [HttpGet("pending/{deviceId}")]
    public ActionResult<List<SignagePushCommand>> GetPendingCommands(string deviceId)
    {
        var commands = _dataService.GetPendingCommands(deviceId);
        return Ok(commands);
    }

    /// <summary>
    /// 更新命令狀態（設備確認收到）
    /// </summary>
    [HttpPost("{commandId}/status")]
    public ActionResult UpdateCommandStatus(string commandId, [FromBody] UpdateCommandStatusRequest request)
    {
        var success = _dataService.UpdateCommandStatus(commandId, request.Status);
        if (!success)
            return NotFound(new { message = "命令不存在" });

        return Ok(new { message = $"命令狀態已更新為: {request.Status}" });
    }
}

/// <summary>
/// 推送配置請求 DTO
/// </summary>
public class PushConfigurationRequest
{
    public string DeviceId { get; set; } = "";
    public SignagePage? PageConfig { get; set; }
}

/// <summary>
/// 更新命令狀態請求 DTO
/// </summary>
public class UpdateCommandStatusRequest
{
    public string Status { get; set; } = "";
}
