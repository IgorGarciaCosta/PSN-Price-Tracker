using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services.Callbacks;

public class CancelarCallbackHandler : ITelegramCallbackHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;

    public string Prefix => "cancelar:";

    public CancelarCallbackHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
    }

    public async Task HandleAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;

        if (!int.TryParse(data[Prefix.Length..], out int alertaId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        await alertaService.DesativarAlertaAsync(alertaId, chatId);

        await _botApi.SendMessageAsync(botToken, chatId, "✅ Alerta cancelado com sucesso.", ct);
    }
}
