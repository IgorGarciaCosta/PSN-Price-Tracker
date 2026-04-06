namespace PsnPriceTracker.Interfaces
{
    public interface ITelegramIntegrationService
    {
        /// <summary>
        /// Sends a text message to a specific Telegram chat by chatId.
        /// </summary>
        Task SendMessageAsync(long chatId, string message);
    }
}
