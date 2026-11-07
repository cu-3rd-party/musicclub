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

        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            // Сюда долетает только то, что не обработал UseExceptionHandler; Kestrel ответит 500
            failed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = failed && !context.Response.HasStarted
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (statusCode >= StatusCodes.Status400BadRequest)
            {
                // Причину кладёт ApiProblems.Customize, когда ответ пишется как ProblemDetails
                var error = context.Items[ApiProblems.ErrorSummaryKey] as string ?? "без тела ответа";
                logger.Log(
                    statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Warning,
                    "{ClientIp} {Method} {Path} {StatusCode} {ElapsedMs}ms — {Error}",
                    clientIp,
                    method,
                    requestPath,
                    statusCode,
                    elapsedMs,
                    error);
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
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
