using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly ITelegramBotApiService _botApi;
    private readonly TelegramCommandHandler _commandHandler;

    public TelegramBotHostedService(
        IConfiguration configuration,
        ILogger<TelegramBotHostedService> logger,
        ITelegramBotApiService botApi,
        TelegramCommandHandler commandHandler)
    {
        _configuration = configuration;
        _logger = logger;
        _botApi = botApi;
        _commandHandler = commandHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botToken = _configuration["Telegram:BotToken"];

        if (string.IsNullOrEmpty(botToken))
        {
            _logger.LogWarning("Telegram BotToken not configured. Long polling disabled.");
            return;
        }

        _logger.LogInformation("Telegram Bot Long Polling started.");

        long offset = await _botApi.SkipPendingUpdatesAsync(botToken, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botApi.GetUpdatesAsync(botToken, offset, stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.UpdateId + 1;
                    await _commandHandler.ProcessUpdateAsync(botToken, update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no long polling do Telegram. Tentando novamente em 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
