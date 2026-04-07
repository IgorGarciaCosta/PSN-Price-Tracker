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
                "⚠️ Preço inválido. Digite apenas o valor numérico.\n\nExemplo: `150` ou `99.90`", ct);
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
                var msg = $"🚨 *ALERTA DE PREÇO PSN!*\n\n"
                        + $"🎮 *Jogo:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                        + $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Seu Alvo:* R$ {precoAlvo}\n\n"
                        + $"🛒 [Comprar na PSN]({pending.UrlDoJogo})";

                await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
            }
            else
            {
                var msg = $"✅ *Alerta salvo!*\n\n"
                        + $"🎮 *Jogo:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                        + $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Seu Alvo:* R$ {precoAlvo}\n\n"
                        + $"🔔 Você será notificado quando o preço atingir seu alvo.\n"
                        + $"Use /meusalertas para ver seus alertas ativos.";

                await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar preço para {Url}", pending.UrlDoJogo);
            await _botApi.SendMessageAsync(botToken, chatId,
                "⚠️ Alerta salvo, mas não foi possível verificar o preço atual. Você será notificado quando o preço atingir seu alvo.", ct);
        }
    }
}
