namespace PsnPriceTracker.Interfaces;

public interface IApiKeyService
{
    Task<string> GerarApiKeyAsync(long telegramChatId);
    Task<bool> ValidarApiKeyAsync(string chave);
}
