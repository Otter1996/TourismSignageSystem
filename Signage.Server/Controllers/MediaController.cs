using Signage.Common;
using Signage.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Signage.Server.Controllers;

/// <summary>
/// 媒體庫和素材管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly SignageDataService _dataService;
    private readonly SecurityService _securityService;

    public MediaController(SignageDataService dataService, SecurityService securityService)
    {
        _dataService = dataService;
        _securityService = securityService;
    }

    /// <summary>
    /// 取得全域媒體素材
    /// </summary>
    [HttpGet]
    public ActionResult<List<MediaContent>> GetAllMedia()
    {
        var media = _dataService.GetAllMedia();
        return Ok(media);
    }

    /// <summary>
    /// 取得指定中心的媒體素材（相容舊流程）
    /// </summary>
    [HttpGet("center/{centerId}")]
    public ActionResult<List<MediaContent>> GetMediaByCenter(string centerId)
    {
        var media = _dataService.GetMediaByCenter(centerId);
        return Ok(media);
    }

    private static readonly string[] AllowedImageMimes =
        ["image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml", "image/avif", "image/bmp"];

    private static readonly string[] AllowedVideoMimes =
        ["video/mp4", "video/webm", "video/ogg"];

    /// <summary>
    /// 上傳/新增媒體素材
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult<MediaContent> AddMedia([FromBody] AddMediaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { message = "媒體 URL 不能為空" });

        // 若為內嵌檔案（data URL），驗證 MIME 類型符合宣告的媒體類型
        if (request.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var mimeEnd = request.Url.IndexOf(';');
            var mime = mimeEnd > 5 ? request.Url[5..mimeEnd] : "";

            if (string.Equals(request.MediaType, "Image", StringComparison.OrdinalIgnoreCase) &&
                !AllowedImageMimes.Contains(mime, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = $"不支援的圖片格式：{mime}。允許格式：JPEG、PNG、GIF、WebP、SVG、AVIF、BMP。" });

            if (string.Equals(request.MediaType, "Video", StringComparison.OrdinalIgnoreCase) &&
                !AllowedVideoMimes.Contains(mime, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = $"不支援的影片格式：{mime}。允許格式：MP4、WebM、Ogg。" });
        }

        var media = _dataService.AddMedia(
            request.Title ?? "未命名媒體",
            request.Url,
            request.MediaType ?? "Image",
            request.Duration > 0 ? request.Duration : 10
        );

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "Media.Create", media.Id, true, $"新增媒體 {media.Title}", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());

        return CreatedAtAction(nameof(GetAllMedia), media);
    }

    /// <summary>
    /// 刪除媒體素材
    /// </summary>
    [HttpDelete("{mediaId}")]
    [Authorize(Roles = "Admin,Operator")]
    public ActionResult DeleteMedia(string mediaId)
    {
        var success = _dataService.DeleteMedia(mediaId);
        if (!success)
            return NotFound(new { message = "媒體素材不存在" });

        _securityService.AddAudit(User.Identity?.Name ?? "unknown", "Media.Delete", mediaId, true, "刪除媒體", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent.ToString());
        return Ok(new { message = "刪除成功" });
    }
}

/// <summary>
/// 新增媒體請求 DTO
/// </summary>
public class AddMediaRequest
{
    public string? Title { get; set; }
    public string Url { get; set; } = "";
    public string? MediaType { get; set; } = "Image";
    public int Duration { get; set; } = 10;
}
