using PsnPriceTracker.Interfaces;

namespace PsnPriceTracker.Integrations
{
    public class TelegramIntegrationService : ITelegramIntegrationService
    {
        public async Task EnviarMensagemAsync(string mensagem)
        {
            //TODO: use HttpClient and Polly to call Telegram API in the future.
            Console.WriteLine($"[SIMULAÇÃO TELEGRAM] Mensagem a ser enviada: {mensagem}");
            await Task.CompletedTask;
        }
    }
}
