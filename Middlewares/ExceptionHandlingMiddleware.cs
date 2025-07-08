using System.Text.Json;
using FluentValidation;

public class ExceptionHandlingMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly RequestDelegate _next;

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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentException => new {error = exception.Message, code = "INVALID_ARGUMENT", status = 400},
            UnauthorizedAccessException => new {error = "Unauthorized", code = "UNAUTHORIZED", status = 401},
            KeyNotFoundException => new {error = "Resource not found", code = "NOT_FOUND", status = 404},
            ValidationException => new {error = exception.Message, code = "VALIDATION_ERROR", status = 400},
            _ => new {error = "Internal server error", code = "INTERNAL_ERROR", status = 500}
        };

        context.Response.StatusCode = response.status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}