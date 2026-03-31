using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class TelegramBotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly ConcurrentDictionary<long, PendingAlert> _pendingAlerts = new();
    private readonly ConcurrentDictionary<long, List<BuscaResultadoDTO>> _searchResults = new();

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
        // Handle callback queries (inline keyboard button clicks)
        if (update.CallbackQuery is not null)
        {
            var callbackChatId = update.CallbackQuery.Message?.Chat?.Id;
            if (callbackChatId is not null)
                await ProcessCallbackQueryAsync(botToken, update.CallbackQuery, callbackChatId.Value, ct);
            return;
        }

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
            case "/buscar":
                await HandleBuscarAsync(botToken, chatId.Value, messageText, ct);
                break;
            case "/meusalertas":
                await HandleMeusAlertasAsync(botToken, chatId.Value, ct);
                break;
            case "/cancelar":
                await HandleCancelarAsync(botToken, chatId.Value, ct);
                break;
            default:
                await HandleTextoLivreAsync(botToken, chatId.Value, messageText, ct);
                break;
        }
    }

    private async Task HandleStartAsync(string botToken, long chatId, CancellationToken ct)
    {
        var mensagem = "👋 *Bem-vindo ao PSN Price Tracker!*\n\n"
                     + "Eu sou o bot que monitora preços de jogos na PSN.\n\n"
                     + "📋 *Comandos disponíveis:*\n"
                     + "/buscar `nome do jogo` — Busca jogos na PSN\n"
                     + "/meusalertas — Lista seus alertas ativos\n"
                     + "/cancelar — Cancela um alerta ativo\n"
                     + "/gerarkey — Gera sua API Key\n\n"
                     + "🎮 Experimente: /buscar God of War";

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

    private async Task HandleBuscarAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        var nomeDoJogo = messageText.Length > "/buscar ".Length
            ? messageText["/buscar ".Length..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(nomeDoJogo))
        {
            await SendTelegramMessageAsync(botToken, chatId, "❓ Use: /buscar `nome do jogo`\n\nExemplo: /buscar The Last of Us", ct);
            return;
        }

        await SendTelegramMessageAsync(botToken, chatId, $"🔍 Buscando *{nomeDoJogo}* na PSN...", ct);

        var resultados = await BuscarJogosViaScopeAsync(nomeDoJogo);

        if (resultados.Count == 0)
        {
            await SendTelegramMessageAsync(botToken, chatId, $"😕 Nenhum jogo encontrado para *{nomeDoJogo}*.", ct);
            return;
        }

        if (resultados.Count == 1)
        {
            var jogo = resultados[0];
            _pendingAlerts[chatId] = new PendingAlert { UrlDoJogo = jogo.UrlDoJogo, NomeDoJogo = jogo.NomeDoJogo };

            await SendGameCardAsync(botToken, chatId, jogo, inlineKeyboard: null, ct);
            await SendTelegramMessageAsync(botToken, chatId, "💰 Qual o seu *preço-alvo*? (ex: 150.00)", ct);
            return;
        }

        // Múltiplos resultados — salva e mostra com botões inline
        _searchResults[chatId] = resultados;

        for (int i = 0; i < resultados.Count; i++)
        {
            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "Escolher este ✅", callback_data = $"buscar:{i}" }
                    }
                }
            };

            await SendGameCardAsync(botToken, chatId, resultados[i], keyboard, ct);
        }
    }

    private async Task ProcessCallbackQueryAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;

        // Responde o callback para remover o loading do botão
        await AnswerCallbackQueryAsync(botToken, callback.Id!, ct);

        if (data.StartsWith("cancelar:"))
        {
            await ProcessCancelarCallbackAsync(botToken, data, chatId, ct);
            return;
        }

        if (!data.StartsWith("buscar:"))
            return;

        if (!int.TryParse(data["buscar:".Length..], out int index))
            return;

        if (!_searchResults.TryRemove(chatId, out var resultados) || index < 0 || index >= resultados.Count)
        {
            await SendTelegramMessageAsync(botToken, chatId, "⚠️ Resultado expirado. Use /buscar novamente.", ct);
            return;
        }

        var jogo = resultados[index];
        _pendingAlerts[chatId] = new PendingAlert { UrlDoJogo = jogo.UrlDoJogo, NomeDoJogo = jogo.NomeDoJogo };

        await SendTelegramMessageAsync(botToken, chatId,
            $"✅ *{jogo.NomeDoJogo}* selecionado!\n\n💰 Qual o seu *preço-alvo*? (ex: 150.00)", ct);
    }

    private async Task HandleTextoLivreAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        if (!_pendingAlerts.TryGetValue(chatId, out var pending))
            return;

        var cleanText = messageText.Trim()
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("r$", "")
            .Trim();

        if (!decimal.TryParse(cleanText, new CultureInfo("pt-BR"), out decimal precoAlvo) || precoAlvo <= 0)
        {
            await SendTelegramMessageAsync(botToken, chatId,
                "⚠️ Preço inválido. Digite apenas o valor numérico.\n\nExemplo: `150` ou `99.90`", ct);
            return;
        }

        _pendingAlerts.TryRemove(chatId, out _);

        // Persiste o alerta no banco para monitoramento contínuo
        await SalvarAlertaViaScopeAsync(chatId, pending.NomeDoJogo, pending.UrlDoJogo, precoAlvo);

        try
        {
            var dadosPsn = await ObterPrecoViaScopeAsync(pending.UrlDoJogo);

            if (dadosPsn.PrecoAtual <= precoAlvo)
            {
                var msg = $"🚨 *ALERTA DE PREÇO PSN!*\n\n"
                        + $"🎮 *Jogo:* {dadosPsn.NomeDoJogo}\n"
                        + $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Seu Alvo:* R$ {precoAlvo}\n\n"
                        + $"🛒 [Comprar na PSN]({pending.UrlDoJogo})";

                await SendTelegramMessageAsync(botToken, chatId, msg, ct);
            }
            else
            {
                var msg = $"✅ *Alerta salvo!*\n\n"
                        + $"🎮 *Jogo:* {dadosPsn.NomeDoJogo}\n"
                        + $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n"
                        + $"🎯 *Seu Alvo:* R$ {precoAlvo}\n\n"
                        + $"🔔 Você será notificado quando o preço atingir seu alvo.\n"
                        + $"Use /meusalertas para ver seus alertas ativos.";

                await SendTelegramMessageAsync(botToken, chatId, msg, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar preço para {Url}", pending.UrlDoJogo);
            await SendTelegramMessageAsync(botToken, chatId,
                "⚠️ Alerta salvo, mas não foi possível verificar o preço atual. Você será notificado quando o preço atingir seu alvo.", ct);
        }
    }

    private async Task HandleMeusAlertasAsync(string botToken, long chatId, CancellationToken ct)
    {
        var alertas = await ObterAlertasPorChatIdViaScopeAsync(chatId);

        if (alertas.Count == 0)
        {
            await SendTelegramMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos no momento.", ct);
            return;
        }

        var msg = "📋 *Seus alertas ativos:*\n\n";
        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            msg += $"{i + 1}. 🎮 *{a.NomeDoJogo}*\n"
                 + $"   🎯 Alvo: R$ {a.PrecoAlvo}\n"
                 + $"   📅 Criado em: {a.CriadoEm:dd/MM/yyyy}\n\n";
        }

        msg += "Use /cancelar para remover um alerta.";
        await SendTelegramMessageAsync(botToken, chatId, msg, ct);
    }

    private async Task HandleCancelarAsync(string botToken, long chatId, CancellationToken ct)
    {
        var alertas = await ObterAlertasPorChatIdViaScopeAsync(chatId);

        if (alertas.Count == 0)
        {
            await SendTelegramMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos para cancelar.", ct);
            return;
        }

        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[]
                    {
                        new { text = "❌ Cancelar", callback_data = $"cancelar:{a.Id}" }
                    }
                }
            };

            var msg = $"🎮 *{a.NomeDoJogo}*\n🎯 Alvo: R$ {a.PrecoAlvo}";
            await SendTelegramMessageWithKeyboardAsync(botToken, chatId, msg, keyboard, ct);
        }
    }

    private async Task ProcessCancelarCallbackAsync(string botToken, string data, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(data["cancelar:".Length..], out int alertaId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        await alertaService.DesativarAlertaAsync(alertaId);

        await SendTelegramMessageAsync(botToken, chatId, "✅ Alerta cancelado com sucesso.", ct);
    }

    private async Task SalvarAlertaViaScopeAsync(long chatId, string nomeDoJogo, string urlDoJogo, decimal precoAlvo)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        await alertaService.CriarAlertaAsync(chatId, nomeDoJogo, urlDoJogo, precoAlvo);
    }

    private async Task<List<AlertaEntity>> ObterAlertasPorChatIdViaScopeAsync(long chatId)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        return await alertaService.ObterAlertasPorChatIdAsync(chatId);
    }

    private async Task<List<BuscaResultadoDTO>> BuscarJogosViaScopeAsync(string nomeDoJogo)
    {
        using var scope = _scopeFactory.CreateScope();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        return await psnService.BuscarJogosPorNomeAsync(nomeDoJogo);
    }

    private async Task<PrecoPsnDTO> ObterPrecoViaScopeAsync(string urlDoJogo)
    {
        using var scope = _scopeFactory.CreateScope();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        return await psnService.GetCurrentPriceAsync(urlDoJogo);
    }

    private async Task SendGameCardAsync(string botToken, long chatId, BuscaResultadoDTO jogo, object? inlineKeyboard, CancellationToken ct)
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

        await SendTelegramMessageWithKeyboardAsync(botToken, chatId, caption, inlineKeyboard, ct);
    }

    private async Task SendPhotoAsync(string botToken, long chatId, string photoUrl, string caption, object? replyMarkup, CancellationToken ct)
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

        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task AnswerCallbackQueryAsync(string botToken, string callbackQueryId, CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/answerCallbackQuery";

        var payload = new { callback_query_id = callbackQueryId };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        await _httpClient.PostAsync(url, content, ct);
    }

    private async Task SendTelegramMessageWithKeyboardAsync(string botToken, long chatId, string message, object? replyMarkup, CancellationToken ct)
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

        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
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

        [JsonPropertyName("callback_query")]
        public TelegramCallbackQuery? CallbackQuery { get; set; }
    }

    private class TelegramCallbackQuery
    {
        public string? Id { get; set; }
        public string? Data { get; set; }
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

    private class PendingAlert
    {
        public string UrlDoJogo { get; set; } = string.Empty;
        public string NomeDoJogo { get; set; } = string.Empty;
    }
}
