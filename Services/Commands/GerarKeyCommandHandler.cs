using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class GerarKeyCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;
    private readonly ILogger<GerarKeyCommandHandler> _logger;

    public string Command => "/gerarkey";

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
            var aviso = "⚠️ Você já possui uma API Key gerada.\n\n"
                + "Por segurança, a chave só é exibida no momento da criação.\n"
                + "Caso tenha perdido, entre em contato para regenerar.";
            await _botApi.SendMessageAsync(botToken, chatId, aviso, ct);
            return;
        }

        var mensagem = "🔑 *Sua API Key foi gerada com sucesso!*\n\n"
             + $"`{chave}`\n\n"
             + "📋 *Como usar:*\n"
             + "Adicione o seguinte Header em todas as requisições:\n\n"
             + "*Header:* `X-Api-Key`\n"
             + $"*Valor:* `{chave}`\n\n"
             + "🔒 Guarde esta chave com segurança. Ela é única e intransferível.";

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);

        _logger.LogInformation("API Key gerada para o chat {ChatId}", chatId);
    }
}
