using System.Diagnostics;

namespace CuMusicClub.Web.Middleware;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly ActivitySource ActivitySource = new("CuMusicClub.Web");

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;
        var requestPath = request.Path + request.QueryString;
        var method = request.Method;
        var clientIp = GetClientIp(context);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (statusCode != 200)
            {
                var responseBody = await ReadResponseBodyAsync(context);
                logger.LogWarning(
                    "{ClientIp} {Method} {Path} {StatusCode} {ElapsedMs}ms — {Error}",
                    clientIp,
                    method,
                    requestPath,
                    statusCode,
                    elapsedMs,
                    responseBody);
            }
            else
            {
                logger.LogInformation(
                    "{ClientIp} {Method} {Path} {StatusCode} {ElapsedMs}ms",
                    clientIp,
                    method,
                    requestPath,
                    statusCode,
                    elapsedMs);
            }
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',').First().Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "-";
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        try
        {
            if (context.Response.Body.CanSeek)
            {
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                return body.Length > 500 ? body[..500] + "..." : body;
            }
        }
        catch
        {
            // Игнорируем ошибки чтения тела ответа
        }

        return string.Empty;
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
