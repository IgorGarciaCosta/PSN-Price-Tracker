using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class AlertaMonitorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertaMonitorBackgroundService> _logger;

    public AlertaMonitorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AlertaMonitorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertaMonitor started. Waiting for first cycle...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervaloMinutos = _configuration.GetValue("Monitoramento:IntervaloMinutos", 60);
            await Task.Delay(TimeSpan.FromMinutes(intervaloMinutos), stoppingToken);

            await VerificarAlertasAsync(stoppingToken);
        }
    }

    private async Task VerificarAlertasAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting alert verification cycle...");

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramBotApiService>();

        var alertas = await alertaService.ObterAlertasAtivosAsync();

        if (alertas.Count == 0)
        {
            _logger.LogInformation("No active alerts found.");
            return;
        }

        var alertasPorUrl = alertas.GroupBy(a => a.UrlDoJogo);
        _logger.LogInformation("Checking {Count} active alert(s) for {UrlCount} unique game(s)...",
            alertas.Count, alertasPorUrl.Count());

        foreach (var grupo in alertasPorUrl)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var dadosPsn = await psnService.GetCurrentPriceAsync(grupo.Key);

                foreach (var alerta in grupo)
                {
                    await ProcessarAlertaAsync(alerta, dadosPsn, alertaService, telegramService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching price for URL {Url}", grupo.Key);
            }

            // Rate limiting: 2s entre consultas à PSN (por URL única)
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        _logger.LogInformation("Verification cycle completed.");
    }

    private async Task ProcessarAlertaAsync(
        AlertaEntity alerta,
        PrecoPsnDTO dadosPsn,
        IAlertaService alertaService,
        ITelegramBotApiService telegramService)
    {
        try
        {
            if (dadosPsn.PrecoAtual <= alerta.PrecoAlvo)
            {
                var mensagem = $"🚨 *PSN PRICE ALERT!*\n\n"
                             + $"🎮 *Game:* {Helpers.MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                             + $"💰 *Current Price:* R$ {dadosPsn.PrecoAtual}\n"
                             + $"🎯 *Your Target:* R$ {alerta.PrecoAlvo}\n\n"
                             + $"🛒 [Buy on PSN]({alerta.UrlDoJogo})";

                await telegramService.SendMessageAsync(alerta.TelegramChatId, mensagem);
                await alertaService.MarcarComoNotificadoAsync(alerta.Id);

                _logger.LogInformation("Alerta {Id} notificado para chat {ChatId} — {Jogo}",
                    alerta.Id, alerta.TelegramChatId, alerta.NomeDoJogo);
            }
            else
            {
                _logger.LogDebug("Alerta {Id} — {Jogo}: R$ {PrecoAtual} > R$ {PrecoAlvo}",
                    alerta.Id, alerta.NomeDoJogo, dadosPsn.PrecoAtual, alerta.PrecoAlvo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar alerta {Id} — {Jogo}", alerta.Id, alerta.NomeDoJogo);
        }
    }
}
