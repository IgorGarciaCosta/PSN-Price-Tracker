using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class GerarKeyCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;
    private readonly ILogger<GerarKeyCommandHandler> _logger;

    public string Command => "/apikey";

    public GerarKeyCommandHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi, ILogger<GerarKeyCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
        _logger = logger;
    }

    public async Task ExecuteAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var chave = await apiKeyService.GerarApiKeyAsync(chatId);

        if (string.IsNullOrEmpty(chave))
        {
            var aviso = "⚠️ You already have a generated API Key.\n\n"
                + "For security, the key is only displayed at creation time.\n"
                + "If you lost it, please contact us to regenerate.";
            await _botApi.SendMessageAsync(botToken, chatId, aviso, ct);
            return;
        }

        var mensagem = "🔑 *Your API Key was generated successfully!*\n\n"
             + $"`{chave}`\n\n"
             + "📋 *How to use:*\n"
             + "Add the following Header to all requests:\n\n"
             + "*Header:* `X-Api-Key`\n"
             + $"*Value:* `{chave}`\n\n"
             + "🔒 Keep this key safe. It is unique and non-transferable.";

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);

        _logger.LogInformation("API Key gerada para o chat {ChatId}", chatId);
    }
}
