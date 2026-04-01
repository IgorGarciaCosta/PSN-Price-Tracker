using System.Text.Json;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class TelegramBotApiService : ITelegramBotApiService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramBotApiService> _logger;

    public TelegramBotApiService(IHttpClientFactory httpClientFactory, ILogger<TelegramBotApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<long> SkipPendingUpdatesAsync(string botToken, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset=-1&timeout=0";
        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TelegramResponse>(json, _jsonOptions);

        var updates = result?.Result ?? [];

        if (updates.Count > 0)
        {
            var lastId = updates[^1].UpdateId;
            _logger.LogInformation("Descartados {Count} updates antigos. Offset inicial: {Offset}", updates.Count, lastId + 1);
            return lastId + 1;
        }

        return 0;
    }

    public async Task<List<TelegramUpdate>> GetUpdatesAsync(string botToken, long offset, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={offset}&timeout=30";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(35));

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        var result = JsonSerializer.Deserialize<TelegramResponse>(json, _jsonOptions);

        return result?.Result ?? [];
    }

    public async Task SendMessageAsync(string botToken, long chatId, string message, CancellationToken ct)
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

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendMessageWithKeyboardAsync(string botToken, long chatId, string message, object? replyMarkup, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = message,
            ["parse_mode"] = "Markdown"
        };

        if (replyMarkup is not null)
            payload["reply_markup"] = replyMarkup;

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendPhotoAsync(string botToken, long chatId, string photoUrl, string caption, object? replyMarkup, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["photo"] = photoUrl,
            ["caption"] = caption,
            ["parse_mode"] = "Markdown"
        };

        if (replyMarkup is not null)
            payload["reply_markup"] = replyMarkup;

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendGameCardAsync(string botToken, long chatId, BuscaResultadoDTO jogo, object? inlineKeyboard, CancellationToken ct)
    {
        var caption = $"🎮 *{jogo.NomeDoJogo}*\n"
                    + $"🕹️ {jogo.Plataforma ?? "N/A"}\n"
                    + $"💰 {jogo.PrecoAtual ?? "Preço indisponível"}";

        if (!string.IsNullOrEmpty(jogo.ImagemUrl))
        {
            try
            {
                await SendPhotoAsync(botToken, chatId, jogo.ImagemUrl, caption, inlineKeyboard, ct);
                return;
            }
            catch
            {
                // Fallback para texto se a foto falhar
            }
        }

        await SendMessageWithKeyboardAsync(botToken, chatId, caption, inlineKeyboard, ct);
    }

    public async Task AnswerCallbackQueryAsync(string botToken, string callbackQueryId, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/answerCallbackQuery";

        var payload = new { callback_query_id = callbackQueryId };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var httpClient = _httpClientFactory.CreateClient();
        await httpClient.PostAsync(url, content, ct);
    }
}
