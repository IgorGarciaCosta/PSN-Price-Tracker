using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class MeusAlertasCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;

    public string Command => "/myalerts";

    public MeusAlertasCommandHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi)
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
            await _botApi.SendMessageAsync(botToken, chatId, "📭 You have no active alerts at the moment.", ct);
            return;
        }

        var msg = "📋 *Your active alerts:*\n\n";
        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            msg += $"{i + 1}. 🎮 *{MarkdownSanitizer.Escape(a.NomeDoJogo)}*\n"
                 + $"   🎯 Target: R$ {a.PrecoAlvo}\n"
                 + $"   📅 Created on: {a.CriadoEm:MM/dd/yyyy}\n\n";
        }

        msg += "Use /cancel to remove an alert.";
        await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
    }
}
