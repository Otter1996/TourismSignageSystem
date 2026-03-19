using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Signage.Server.Controllers;

/// <summary>
/// 媒體庫和素材管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly SignageDataService _dataService;

    public MediaController(SignageDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// 取得指定中心的所有媒體素材
    /// </summary>
    [HttpGet("center/{centerId}")]
    public ActionResult<List<MediaContent>> GetMediaByCenter(string centerId)
    {
        var media = _dataService.GetMediaByCenter(centerId);
        return Ok(media);
    }

    /// <summary>
    /// 上傳/新增媒體素材
    /// </summary>
    [HttpPost]
    public ActionResult<MediaContent> AddMedia([FromBody] AddMediaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CenterId))
            return BadRequest(new { message = "中心 ID 不能為空" });

        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { message = "媒體 URL 不能為空" });

        var media = _dataService.AddMedia(
            request.CenterId,
            request.Title ?? "未命名媒體",
            request.Url,
            request.MediaType ?? "Image",
            request.Duration > 0 ? request.Duration : 10
        );

        return CreatedAtAction("GetMediaByCenter", new { centerId = request.CenterId }, media);
    }

    /// <summary>
    /// 刪除媒體素材
    /// </summary>
    [HttpDelete("{mediaId}")]
    public ActionResult DeleteMedia(string mediaId)
    {
        var success = _dataService.DeleteMedia(mediaId);
        if (!success)
            return NotFound(new { message = "媒體素材不存在" });
        return Ok(new { message = "刪除成功" });
    }
}

/// <summary>
/// 新增媒體請求 DTO
/// </summary>
public class AddMediaRequest
{
    public string CenterId { get; set; } = "";
    public string? Title { get; set; }
    public string Url { get; set; } = "";
    public string? MediaType { get; set; } = "Image";
    public int Duration { get; set; } = 10;
}
