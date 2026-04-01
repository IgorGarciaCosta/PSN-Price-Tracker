using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces;

public interface ITelegramBotApiService
{
    Task<List<TelegramUpdate>> GetUpdatesAsync(string botToken, long offset, CancellationToken ct);
    Task<long> SkipPendingUpdatesAsync(string botToken, CancellationToken ct);
    Task SendMessageAsync(string botToken, long chatId, string message, CancellationToken ct);
    Task SendMessageWithKeyboardAsync(string botToken, long chatId, string message, object? replyMarkup, CancellationToken ct);
    Task SendPhotoAsync(string botToken, long chatId, string photoUrl, string caption, object? replyMarkup, CancellationToken ct);
    Task SendGameCardAsync(string botToken, long chatId, BuscaResultadoDTO jogo, object? inlineKeyboard, CancellationToken ct);
    Task AnswerCallbackQueryAsync(string botToken, string callbackQueryId, CancellationToken ct);
}
