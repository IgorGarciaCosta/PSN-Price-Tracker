using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;
using PsnPriceTracker.Services.Commands;

namespace PsnPriceTracker.Services;

public class TelegramCommandHandler
{
    private readonly Dictionary<string, ITelegramCommand> _commands;
    private readonly Dictionary<string, ITelegramCallbackHandler> _callbackHandlers;
    private readonly TextoLivreHandler _textoLivreHandler;
    private readonly ITelegramBotApiService _botApi;
    private readonly TelegramSessionManager _session;
    private readonly ILogger<TelegramCommandHandler> _logger;

    public TelegramCommandHandler(
        IEnumerable<ITelegramCommand> commands,
        IEnumerable<ITelegramCallbackHandler> callbackHandlers,
        TextoLivreHandler textoLivreHandler,
        ITelegramBotApiService botApi,
        TelegramSessionManager session,
        ILogger<TelegramCommandHandler> logger)
    {
        _commands = commands.ToDictionary(c => c.Command, StringComparer.OrdinalIgnoreCase);
        _callbackHandlers = callbackHandlers.ToDictionary(h => h.Prefix, StringComparer.OrdinalIgnoreCase);
        _textoLivreHandler = textoLivreHandler;
        _botApi = botApi;
        _session = session;
        _logger = logger;
    }

    public async Task ProcessUpdateAsync(string botToken, TelegramUpdate update, CancellationToken ct)
    {
        _session.CleanupExpired();

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

        _logger.LogInformation("Command received: {Comando} from chat {ChatId}", messageText, chatId);

        var commandKey = messageText.Split(' ')[0].ToLowerInvariant();

        if (_commands.TryGetValue(commandKey, out var handler))
            await handler.ExecuteAsync(botToken, chatId.Value, messageText, ct);
        else
            await _textoLivreHandler.HandleAsync(botToken, chatId.Value, messageText, ct);
    }

    private async Task ProcessCallbackQueryAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;

        await _botApi.AnswerCallbackQueryAsync(botToken, callback.Id!, ct);

        foreach (var (prefix, handler) in _callbackHandlers)
        {
            if (data.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await handler.HandleAsync(botToken, callback, chatId, ct);
                return;
            }
        }
    }
}
