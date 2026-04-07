using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class MeusAlertasCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;

    public string Command => "/meusalertas";

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
            await _botApi.SendMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos no momento.", ct);
            return;
        }

        var msg = "📋 *Seus alertas ativos:*\n\n";
        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            msg += $"{i + 1}. 🎮 *{MarkdownSanitizer.Escape(a.NomeDoJogo)}*\n"
                 + $"   🎯 Alvo: R$ {a.PrecoAlvo}\n"
                 + $"   📅 Criado em: {a.CriadoEm:dd/MM/yyyy}\n\n";
        }

        msg += "Use /cancelar para remover um alerta.";
        await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
    }
}
