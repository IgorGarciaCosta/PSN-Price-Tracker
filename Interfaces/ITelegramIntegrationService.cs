namespace PsnPriceTracker.Interfaces
{
    public interface ITelegramIntegrationService
    {
        Task EnviarMensagemAsync(string mensagem);
    }
}
