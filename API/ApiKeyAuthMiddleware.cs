namespace StartRef.Api;

public sealed class ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task Invoke(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var configuredApiKey = configuration["ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Server API key is not configured." });
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedApiKey) ||
            !string.Equals(providedApiKey.ToString(), configuredApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        await next(context);
    }
}
