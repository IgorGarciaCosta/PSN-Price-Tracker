using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces;

public interface ITelegramCallbackHandler
{
    string Prefix { get; }
    Task HandleAsync(string botToken, TelegramCallbackQuery callback, long chatId, CancellationToken ct);
}
