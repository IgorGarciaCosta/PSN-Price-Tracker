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
        _logger.LogInformation("AlertaMonitor iniciado. Aguardando primeiro ciclo...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervaloMinutos = _configuration.GetValue("Monitoramento:IntervaloMinutos", 60);
            await Task.Delay(TimeSpan.FromMinutes(intervaloMinutos), stoppingToken);

            await VerificarAlertasAsync(stoppingToken);
        }
    }

    private async Task VerificarAlertasAsync(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando ciclo de verificação de alertas...");

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramIntegrationService>();

        var alertas = await alertaService.ObterAlertasAtivosAsync();

        if (alertas.Count == 0)
        {
            _logger.LogInformation("Nenhum alerta ativo encontrado.");
            return;
        }

        var alertasPorUrl = alertas.GroupBy(a => a.UrlDoJogo);
        _logger.LogInformation("Verificando {Count} alerta(s) ativo(s) para {UrlCount} jogo(s) único(s)...",
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
                _logger.LogError(ex, "Erro ao buscar preço para URL {Url}", grupo.Key);
            }

            // Rate limiting: 2s entre consultas à PSN (por URL única)
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        _logger.LogInformation("Ciclo de verificação concluído.");
    }

    private async Task ProcessarAlertaAsync(
        AlertaEntity alerta,
        PrecoPsnDTO dadosPsn,
        IAlertaService alertaService,
        ITelegramIntegrationService telegramService)
    {
        try
        {
            if (dadosPsn.PrecoAtual <= alerta.PrecoAlvo)
            {
                var mensagem = $"🚨 *ALERTA DE PREÇO PSN!*\n\n"
                             + $"🎮 *Jogo:* {Helpers.MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n"
                             + $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n"
                             + $"🎯 *Seu Alvo:* R$ {alerta.PrecoAlvo}\n\n"
                             + $"🛒 [Comprar na PSN]({alerta.UrlDoJogo})";

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
