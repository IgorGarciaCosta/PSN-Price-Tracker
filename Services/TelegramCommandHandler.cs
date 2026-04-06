using System.Collections.Concurrent;
using System.Globalization;
using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class TelegramCommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotApiService _botApi;
    private readonly ILogger<TelegramCommandHandler> _logger;
    private readonly ConcurrentDictionary<long, TimestampedEntry<PendingAlert>> _pendingAlerts = new();
    private readonly ConcurrentDictionary<long, TimestampedEntry<List<BuscaResultadoDTO>>> _searchResults = new();
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    public TelegramCommandHandler(
        IServiceScopeFactory scopeFactory,
        ITelegramBotApiService botApi,
        ILogger<TelegramCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _botApi = botApi;
        _logger = logger;
    }

    public async Task ProcessUpdateAsync(string botToken, TelegramUpdate update, CancellationToken ct)
    {
        CleanupExpiredEntries();

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

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);
    }

    private async Task HandleGerarKeyAsync(string botToken, long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var chave = await apiKeyService.GerarApiKeyAsync(chatId);

        if (string.IsNullOrEmpty(chave))
        {
            var aviso = "⚠️ Você já possui uma API Key gerada.\n\n"
                + "Por segurança, a chave só é exibida no momento da criação.\n"
                + "Caso tenha perdido, entre em contato para regenerar.";
            await _botApi.SendMessageAsync(botToken, chatId, aviso, ct);
            return;
        }

        var mensagem = "🔑 *Sua API Key foi gerada com sucesso!*\n\n"
             + $"`{chave}`\n\n"
             + "📋 *Como usar:*\n"
             + "Adicione o seguinte Header em todas as requisições:\n\n"
             + "*Header:* `X-Api-Key`\n"
             + $"*Valor:* `{chave}`\n\n"
             + "🔒 Guarde esta chave com segurança. Ela é única e intransferível.";

        await _botApi.SendMessageAsync(botToken, chatId, mensagem, ct);

        _logger.LogInformation("API Key gerada para o chat {ChatId}", chatId);
    }

    private async Task HandleBuscarAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        var nomeDoJogo = messageText.Length > "/buscar ".Length
            ? messageText["/buscar ".Length..].Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(nomeDoJogo))
        {
            await _botApi.SendMessageAsync(botToken, chatId, "❓ Use: /buscar `nome do jogo`\n\nExemplo: /buscar The Last of Us", ct);
            return;
        }

        // Limpa estado anterior do mesmo usuário ao iniciar nova busca
        _pendingAlerts.TryRemove(chatId, out _);
        _searchResults.TryRemove(chatId, out _);

        await _botApi.SendMessageAsync(botToken, chatId, $"🔍 Buscando *{MarkdownSanitizer.Escape(nomeDoJogo)}* na PSN...", ct);

        using var scope = _scopeFactory.CreateScope();
        var psnService = scope.ServiceProvider.GetRequiredService<IPsnIntegrationService>();
        var resultados = await psnService.BuscarJogosPorNomeAsync(nomeDoJogo);

        if (resultados.Count == 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId, $"😕 Nenhum jogo encontrado para *{MarkdownSanitizer.Escape(nomeDoJogo)}*.", ct);
            return;
        }

        if (resultados.Count == 1)
        {
            var jogo = resultados[0];
            _pendingAlerts[chatId] = new TimestampedEntry<PendingAlert>(new PendingAlert { UrlDoJogo = jogo.UrlDoJogo, NomeDoJogo = jogo.NomeDoJogo });

            await _botApi.SendGameCardAsync(botToken, chatId, jogo, inlineKeyboard: null, ct);
            await _botApi.SendMessageAsync(botToken, chatId, "💰 Qual o seu *preço-alvo*? (ex: 150.00)", ct);
            return;
        }

        _searchResults[chatId] = new TimestampedEntry<List<BuscaResultadoDTO>>(resultados);

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

            await _botApi.SendGameCardAsync(botToken, chatId, resultados[i], keyboard, ct);
        }
    }

    private async Task ProcessCallbackQueryAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;

        await _botApi.AnswerCallbackQueryAsync(botToken, callback.Id!, ct);

        if (data.StartsWith("cancelar:"))
        {
            await ProcessCancelarCallbackAsync(botToken, data, chatId, ct);
            return;
        }

        if (!data.StartsWith("buscar:"))
            return;

        if (!int.TryParse(data["buscar:".Length..], out int index))
            return;

        if (!_searchResults.TryRemove(chatId, out var entry) || entry.IsExpired(EntryTtl) || index < 0 || index >= entry.Value.Count)
        {
            await _botApi.SendMessageAsync(botToken, chatId, "⚠️ Resultado expirado. Use /buscar novamente.", ct);
            return;
        }

        var jogo = entry.Value[index];
        _pendingAlerts[chatId] = new TimestampedEntry<PendingAlert>(new PendingAlert { UrlDoJogo = jogo.UrlDoJogo, NomeDoJogo = jogo.NomeDoJogo });

        await _botApi.SendMessageAsync(botToken, chatId,
            $"✅ *{MarkdownSanitizer.Escape(jogo.NomeDoJogo)}* selecionado!\n\n💰 Qual o seu *preço-alvo*? (ex: 150.00)", ct);
    }

    private async Task HandleTextoLivreAsync(string botToken, long chatId, string messageText, CancellationToken ct)
    {
        if (!_pendingAlerts.TryGetValue(chatId, out var entry) || entry.IsExpired(EntryTtl))
        {
            _pendingAlerts.TryRemove(chatId, out _);
            return;
        }

        var pending = entry.Value;

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

        _pendingAlerts.TryRemove(chatId, out _);

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

    private async Task HandleMeusAlertasAsync(string botToken, long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        var alertas = await alertaService.ObterAlertasPorChatIdAsync(chatId);

        if (alertas.Count == 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos no momento.", ct);
            return;
        }

        var msg = "📋 *Seus alertas ativos:*\n\n";
        for (int i = 0; i < alertas.Count; i++)
        {
            var a = alertas[i];
            msg += $"{i + 1}. 🎮 *{MarkdownSanitizer.Escape(a.NomeDoJogo)}*\n"
                 + $"   🎯 Alvo: R$ {a.PrecoAlvo}\n"
                 + $"   📅 Criado em: {a.CriadoEm:dd/MM/yyyy}\n\n";
        }

        msg += "Use /cancelar para remover um alerta.";
        await _botApi.SendMessageAsync(botToken, chatId, msg, ct);
    }

    private async Task HandleCancelarAsync(string botToken, long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        var alertas = await alertaService.ObterAlertasPorChatIdAsync(chatId);

        if (alertas.Count == 0)
        {
            await _botApi.SendMessageAsync(botToken, chatId, "📭 Você não tem alertas ativos para cancelar.", ct);
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

            var msg = $"🎮 *{MarkdownSanitizer.Escape(a.NomeDoJogo)}*\n🎯 Alvo: R$ {a.PrecoAlvo}";
            await _botApi.SendMessageWithKeyboardAsync(botToken, chatId, msg, keyboard, ct);
        }
    }

    private async Task ProcessCancelarCallbackAsync(string botToken, string data, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(data["cancelar:".Length..], out int alertaId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var alertaService = scope.ServiceProvider.GetRequiredService<IAlertaService>();
        await alertaService.DesativarAlertaAsync(alertaId, chatId);

        await _botApi.SendMessageAsync(botToken, chatId, "✅ Alerta cancelado com sucesso.", ct);
    }

    public class PendingAlert
    {
        public string UrlDoJogo { get; set; } = string.Empty;
        public string NomeDoJogo { get; set; } = string.Empty;
    }

    private sealed record TimestampedEntry<T>(T Value)
    {
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public bool IsExpired(TimeSpan ttl) => DateTime.UtcNow - CreatedAtUtc > ttl;
    }

    private void CleanupExpiredEntries()
    {
        foreach (var key in _pendingAlerts.Keys)
        {
            if (_pendingAlerts.TryGetValue(key, out var pa) && pa.IsExpired(EntryTtl))
                _pendingAlerts.TryRemove(key, out _);
        }

        foreach (var key in _searchResults.Keys)
        {
            if (_searchResults.TryGetValue(key, out var sr) && sr.IsExpired(EntryTtl))
                _searchResults.TryRemove(key, out _);
        }
    }
}
