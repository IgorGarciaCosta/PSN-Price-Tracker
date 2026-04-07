using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services.Commands;

public class BuscarCommandHandler : ITelegramCommand
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;
    private readonly TelegramSessionManager _session;

    public string Command => "/buscar";

    public BuscarCommandHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi, TelegramSessionManager session)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
        _session = session;
    }

    public async Task ExecuteAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        var nomeDoJogo = messageText.Length > "/buscar ".Length
            ? messageText["/buscar ".Length..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(nomeDoJogo))
        {
            await _botApi.SendMessageAsync(botToken, chatId, "❓ Usage: /buscar `game name`\n\nExample: /buscar The Last of Us", ct);
            return;
        }

        _session.ClearSession(chatId);

        await _botApi.SendMessageAsync(botToken, chatId, $"🔍 Searching for *{MarkdownSanitizer.Escape(nomeDoJogo)}* on PSN...", ct);

        using var scope = _scopeFactory.CreateScope();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        var resultados = await psnService.BuscarJogosPorNomeAsync(nomeDoJogo);

        if (resultados.Count == 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId, $"😕 No games found for *{MarkdownSanitizer.Escape(nomeDoJogo)}*.", ct);
            return;
        }

        if (resultados.Count == 1)
        {
            var jogo = resultados[0];
            _session.SetPendingAlert(chatId, jogo.UrlDoJogo, jogo.NomeDoJogo);

            await _botApi.SendGameCardAsync(botToken, chatId, jogo, inlineKeyboard: null, ct);
            await _botApi.SendMessageAsync(botToken, chatId, "💰 What is your *target price*? (e.g. 150.00)", ct);
            return;
        }

        _session.SetSearchResults(chatId, resultados);

        for (int i = 0; i < resultados.Count; i++)
        {
            var keyboard = new InlineKeyboardMarkup(
                new InlineKeyboardButton { Text = "Select this ✅", CallbackData = $"buscar:{i}" });

            await _botApi.SendGameCardAsync(botToken, chatId, resultados[i], keyboard, ct);
        }
    }
}
