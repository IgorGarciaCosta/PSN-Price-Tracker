using System.Globalization;
using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services.Commands;

public class TextoLivreHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;
    private readonly TelegramSessionManager _session;
    private readonly ILogger<TextoLivreHandler> _logger;

    public TextoLivreHandler(IServiceScopeFactory scopeFactory, ITelegramBotApiService botApi, TelegramSessionManager session, ILogger<TextoLivreHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
        _session = session;
        _logger = logger;
    }

    public async Task HandleAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        var pending = _session.TryGetPendingAlert(chatId);
        if (pending is null)
            return;

        var cleanText = messageText.Trim()
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("r$", "")
            .Trim();

        if (!decimal.TryParse(cleanText, new CultureInfo("pt-BR"), out decimal precoAlvo) || precoAlvo <= 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId,
                "⚠️ Invalid price. Enter only the numeric value.\n\nExample: `150` or `99.90`", ct);
            return;
        }

        _session.RemovePendingAlert(chatId);

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        await alertaService.CriarAlertaAsync(chatId, pending.NomeDoJogo, pending.UrlDoJogo, precoAlvo);

        try
        {
            var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
            var dadosPsn = await psnService.GetCurrentPriceAsync(pending.UrlDoJogo);

            if (dadosPsn.PrecoAtual <= precoAlvo)
            {
                var msg = $"🚨 *PSN PRICE ALERT!*\n\n"
                        + $"🎮 *Game:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                        + $"💰 *Current Price:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Your Target:* R$ {precoAlvo}\n\n"
                        + $"🛒 [Buy on PSN]({pending.UrlDoJogo})";

                await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
            }
            else
            {
                var msg = $"✅ *Alert saved!*\n\n"
                        + $"🎮 *Game:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                        + $"💰 *Current Price:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Your Target:* R$ {precoAlvo}\n\n"
                        + $"🔔 You will be notified when the price reaches your target.\n"
                        + $"Use /meusalertas to see your active alerts.";

                await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price for {Url}", pending.UrlDoJogo);
            await _botApi.SendMessageAsync(botToken, chatId,
                "⚠️ Alert saved, but we couldn't check the current price. You will be notified when the price reaches your target.", ct);
        }
    }
}
