namespace PsnPriceTracker.Interfaces
{
    public interface ITelegramIntegrationService
    {
        /// <summary>
        /// Sends a text message to the configured Telegram chat using the Bot API.
        /// </summary>
        Task SendMessageAsync(string message);
    }
}
