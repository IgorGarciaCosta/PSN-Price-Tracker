using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services.Commands;

public class CancelarCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;

    public string Command => "/cancelar";

    public CancelarCommandHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
    }

    public async Task ExecuteAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        var alertas = await alertaService.ObterAlertasPorChatIdAsync(chatId);

        if (alertas.Count == 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos para cancelar.", ct);
            return;
        }

        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            var keyboard = new InlineKeyboardMarkup(
                new InlineKeyboardButton { Text = "❌ Cancelar", CallbackData = $"cancelar:{a.Id}" });

            var msg = $"🎮 *{MarkdownSanitizer.Escape(a.NomeDoJogo)}*\n🎯 Alvo: R$ {a.PrecoAlvo}";
            await _botApi.SendMessageWithKeyboardAsync(botToken, chatId, msg, keyboard, ct);
        }
    }
}
