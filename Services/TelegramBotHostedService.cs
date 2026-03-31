using System.Text.Json;
using System.Text.Json.Serialization;
using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly HttpClient _httpClient = new();

    public TelegramBotHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TelegramBotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botToken = _configuration["Telegram:BotToken"];

        if (string.IsNullOrEmpty(botToken))
        {
            _logger.LogWarning("Telegram BotToken não configurado. Long polling desativado.");
            return;
        }

        _logger.LogInformation("Telegram Bot Long Polling iniciado.");

        // Descarta todos os updates antigos para não reprocessar mensagens anteriores
        long offset = await SkipPendingUpdatesAsync(botToken, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await GetUpdatesAsync(botToken, offset, stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.UpdateId + 1;
                    await ProcessUpdateAsync(botToken, update, stoppingToken);
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

    private async Task<long> SkipPendingUpdatesAsync(string botToken, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset=-1&timeout=0";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TelegramResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var updates = result?.Result ?? [];

        if (updates.Count > 0)
        {
            var lastId = updates[^1].UpdateId;
            _logger.LogInformation("Descartados {Count} updates antigos. Offset inicial: {Offset}", updates.Count, lastId + 1);
            return lastId + 1;
        }

        return 0;
    }

    private async Task<List<TelegramUpdate>> GetUpdatesAsync(string botToken, long offset, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={offset}&timeout=30";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(35));

        var response = await _httpClient.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        var result = JsonSerializer.Deserialize<TelegramResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result?.Result ?? [];
    }

    private async Task ProcessUpdateAsync(string botToken, TelegramUpdate update, CancellationToken ct)
    {
        var messageText = update.Message?.Text;
        var chatId = update.Message?.Chat?.Id;

        if (string.IsNullOrEmpty(messageText) || chatId is null)
            return;

        _logger.LogInformation("Comando recebido: {Comando} do chat {ChatId}", messageText, chatId);

        var command = messageText.Split(' ')[0].ToLowerInvariant();

        switch (command)
        {
            case "/start":
                await HandleStartAsync(botToken, chatId.Value, ct);
                break;
            case "/gerarkey":
                await HandleGerarKeyAsync(botToken, chatId.Value, ct);
                break;
        }
    }

    private async Task HandleStartAsync(string botToken, long chatId, CancellationToken ct)
    {
        var mensagem = "👋 *Bem-vindo ao PSN Price Tracker!*\n\n"
                     + "Eu sou o bot que monitora preços de jogos na PSN.\n\n"
                     + "🔑 Para usar a API, você precisa de uma chave de acesso.\n"
                     + "Digite /gerarkey para receber a sua API Key.";

        await SendTelegramMessageAsync(botToken, chatId, mensagem, ct);
    }

    private async Task HandleGerarKeyAsync(string botToken, long chatId, CancellationToken ct)
    {
        var chave = await GerarApiKeyViaScopeAsync(chatId);
        var mensagem = BuildApiKeyMessage(chave);

        await SendTelegramMessageAsync(botToken, chatId, mensagem, ct);

        _logger.LogInformation("API Key gerada para o chat {ChatId}", chatId);
    }

    private async Task<string> GerarApiKeyViaScopeAsync(long chatId)
    {
        using var scope = _scopeFactory.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        return await apiKeyService.GerarApiKeyAsync(chatId);
    }

    private static string BuildApiKeyMessage(string chave)
    {
        return "🔑 *Sua API Key foi gerada com sucesso!*\n\n"
             + $"`{chave}`\n\n"
             + "📋 *Como usar:*\n"
             + "Adicione o seguinte Header em todas as requisições:\n\n"
             + "*Header:* `X-Api-Key`\n"
             + $"*Valor:* `{chave}`\n\n"
             + "🔒 Guarde esta chave com segurança. Ela é única e intransferível.";
    }

    private async Task SendTelegramMessageAsync(string botToken, long chatId, string message, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = message,
            parse_mode = "Markdown"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    // DTOs internos para deserializar a resposta do Telegram
    private class TelegramResponse
    {
        public bool Ok { get; set; }
        public List<TelegramUpdate>? Result { get; set; }
    }

    private class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }
        public TelegramMessage? Message { get; set; }
    }

    private class TelegramMessage
    {
        public string? Text { get; set; }
        public TelegramChat? Chat { get; set; }
    }

    private class TelegramChat
    {
        public long? Id { get; set; }
    }
}
