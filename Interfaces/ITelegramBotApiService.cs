using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces;

public interface ITelegramBotApiService
{
    Task<List<TelegramUpdate>> GetUpdatesAsync(string botToken, long offset, CancellationToken ct);
    Task<long> SkipPendingUpdatesAsync(string botToken, CancellationToken ct);
    Task SendMessageAsync(string botToken, long chatId, string message, CancellationToken ct);
    Task SendMessageAsync(long chatId, string message);
    Task SendMessageWithKeyboardAsync(string botToken, long chatId, string message, InlineKeyboardMarkup? replyMarkup, CancellationToken ct);
    Task SendPhotoAsync(string botToken, long chatId, string photoUrl, string caption, InlineKeyboardMarkup? replyMarkup, CancellationToken ct);
    Task SendGameCardAsync(string botToken, long chatId, BuscaResultadoDTO jogo, InlineKeyboardMarkup? inlineKeyboard, CancellationToken ct);
    Task AnswerCallbackQueryAsync(string botToken, string callbackQueryId, CancellationToken ct);
}
