using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string APIKEYNAME = "X-Api-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/swagger") || path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(APIKEYNAME, out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Mensagem = "Acesso negado. API Key não fornecida no Header." });
            return;
        }

        var apiKeyService = context.RequestServices.GetRequiredService<IApiKeyService>();
        var chatId = await apiKeyService.ObterChatIdPorApiKeyAsync(extractedApiKey!);

        if (chatId is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Mensagem = "Acesso negado. API Key inválida." });
            return;
        }

        context.Items["TelegramChatId"] = chatId.Value;

        await _next(context);
    }
}