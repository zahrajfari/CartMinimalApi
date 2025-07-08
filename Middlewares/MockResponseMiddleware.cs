using System.Text.Json;

public class MockResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Random _random = new();

    public MockResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for force error header
        if (context.Request.Headers.ContainsKey("X-Force-Error"))
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new {error = "Forced error for testing"}));
            return;
        }

        // Check for delay header
        if (context.Request.Headers.TryGetValue("X-Delay-Response", out var delayValue) &&
            int.TryParse(delayValue, out var delay))
            await Task.Delay(delay);

        // Random failure simulation (5% chance)
        if (_random.NextDouble() < 0.05 && !context.Request.Path.StartsWithSegments("/health"))
        {
            var errors = new[]
            {
                new {error = "Service temporarily unavailable", code = "SERVICE_UNAVAILABLE"},
                new {error = "Database connection timeout", code = "DB_TIMEOUT"},
                new {error = "Rate limit exceeded", code = "RATE_LIMIT_EXCEEDED"}
            };

            var randomError = errors[_random.Next(errors.Length)];
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(randomError));
            return;
        }

        await _next(context);
    }
}