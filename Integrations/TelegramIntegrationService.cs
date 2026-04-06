using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Integrations
{
    public class TelegramIntegrationService : ITelegramIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        //injected HttpClient and IConfiguration to access Telegram API settings from appsettings.json
        public TelegramIntegrationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        /// <summary>
        /// Sends a text message to a specific Telegram chat by chatId.
        /// </summary>
        public async Task SendMessageAsync(long chatId, string message)
        {
            var botToken = _configuration["Telegram:BotToken"];
            if (string.IsNullOrEmpty(botToken))
                throw new ArgumentException("O BotToken do Telegram não está configurado no appsettings.json.");

            var url = BuildApiUrl(botToken);
            var content = BuildMessagePayload(chatId.ToString(), message);

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Builds the Telegram Bot API endpoint URL for sending messages.
        /// </summary>
        private string BuildApiUrl(string botToken)
        {
            return $"https://api.telegram.org/bot{botToken}/sendMessage";
        }

        /// <summary>
        /// Serializes the message payload as JSON content ready for the HTTP POST request.
        /// Uses Markdown parse mode to support bold and italic formatting.
        /// </summary>
        private StringContent BuildMessagePayload(string chatId, string message)
        {
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            };

            return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }
    }
}
