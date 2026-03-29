using System.Net;
using System.Text.Json;

namespace Signage.Server.Middleware;

/// <summary>
/// 全域例外處理中間件：遮蔽原始錯誤細節，防止技術資訊洩漏（OWASP A05）
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            // 僅在伺服器端記錄完整錯誤，絕不回傳原始訊息給用戶端
            _logger.LogError(ex, "未預期的系統錯誤，ErrorId={ErrorId}, Path={Path}", errorId, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = JsonSerializer.Serialize(new
            {
                errorCode = $"ERR-{errorId}",
                message = "系統發生錯誤，請記錄錯誤代碼並聯繫管理員"
            });

            await context.Response.WriteAsync(response);
        }
    }
}
