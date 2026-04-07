using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services.Callbacks;

public class BuscarCallbackHandler : ITelegramCallbackHandler
{
    private readonly ITelegramBotApiService _botApi;
    private readonly TelegramSessionManager _session;

    public string Prefix => "buscar:";

    public BuscarCallbackHandler(ITelegramBotApiService botApi, TelegramSessionManager session)
    {
        _botApi = botApi;
        _session = session;
    }

    public async Task HandleAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;

        if (!int.TryParse(data[Prefix.Length..], out int index))
            return;

        var resultados = _session.TryRemoveSearchResults(chatId);
        if (resultados is null || index < 0 || index >= resultados.Count)
        {
            await _botApi.SendMessageAsync(botToken, chatId, "⚠️ Result expired. Use /buscar again.", ct);
            return;
        }

        var jogo = resultados[index];
        _session.SetPendingAlert(chatId, jogo.UrlDoJogo, jogo.NomeDoJogo);

        await _botApi.SendMessageAsync(botToken, chatId,
            $"✅ *{MarkdownSanitizer.Escape(jogo.NomeDoJogo)}* selected!\n\n💰 What is your *target price*? (e.g. 150.00)", ct);
    }
}
