using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Infrastructure;

/// <summary>
/// Единый формат ошибок API: ProblemDetails с русскими title/detail. Фронт показывает detail как есть.
/// </summary>
public static class ApiProblems
{
    /// <summary>Ключ HttpContext.Items, через который причина ошибки попадает в строку RequestLoggingMiddleware.</summary>
    public const string ErrorSummaryKey = "CuMusicClub.ErrorSummary";

    public static ProblemHttpResult BadRequest(string detail) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    public static ProblemHttpResult Validation(string field, string message) =>
        TypedResults.Problem(new HttpValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] })
        {
            Status = StatusCodes.Status400BadRequest,
        });

    /// <summary>Вызывается для каждого ответа, который пишет IProblemDetailsService (TypedResults.Problem, исключения).</summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        // Стандартные заголовки ASP.NET английские («Bad Request», «One or more validation errors occurred.»)
        if (GetTitle(status) is { } title) problem.Title = title;

        if (problem.Detail is null && problem is HttpValidationProblemDetails validation && validation.Errors.Count > 0)
            problem.Detail = string.Join(" ", validation.Errors.SelectMany(e => e.Value));

        // Здесь пользователю уходит общий текст, а в лог — исходное сообщение исключения (тип и ключ объекта и т.п.)
        context.HttpContext.Items[ErrorSummaryKey] = context.Exception is NotFoundException or BadHttpRequestException
            ? context.Exception.Message
            : problem.Detail ?? problem.Title;
    }

    private static string? GetTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Некорректный запрос",
        StatusCodes.Status401Unauthorized => "Требуется вход",
        StatusCodes.Status403Forbidden => "Недостаточно прав",
        StatusCodes.Status404NotFound => "Не найдено",
        StatusCodes.Status409Conflict => "Конфликт",
        StatusCodes.Status413PayloadTooLarge => "Слишком большой файл",
        >= StatusCodes.Status500InternalServerError => "Ошибка сервера",
        _ => null,
    };
}
