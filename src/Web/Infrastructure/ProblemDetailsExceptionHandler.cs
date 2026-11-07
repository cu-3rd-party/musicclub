using CuMusicClub.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CuMusicClub.Web.Infrastructure;

public class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(ve.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
            BookingRuleException be => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                Detail = be.Message,
            },
            BadHttpRequestException bre => new ProblemDetails
            {
                Status = bre.StatusCode,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Detail = "Сервер не смог разобрать запрос. Обновите страницу и попробуйте ещё раз.",
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Detail = "Запрошенный объект не найден — возможно, его уже удалили.",
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                Detail = "Войдите в систему, чтобы продолжить.",
            },
            ForbiddenAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                Detail = "У вас нет прав на это действие.",
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Detail = "Что-то пошло не так на сервере. Попробуйте ещё раз позже.",
            },
        };

        // С .NET 10 ExceptionHandlerMiddleware не логирует исключения, которые обработал IExceptionHandler,
        // поэтому пишем сами: ожидаемые (4xx) попадут в строку RequestLoggingMiddleware, неожиданные — сюда со стектрейсом
        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Необработанное исключение при {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }
}
