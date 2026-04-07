using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces;

public interface ITelegramCommand
{
    string Command { get; }
    Task ExecuteAsync(string botToken, long chatId, string messageText, CancellationToken ct);
}
