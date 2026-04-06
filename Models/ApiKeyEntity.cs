namespace PsnPriceTracker.Models;

public class ApiKeyEntity
{
    public int Id { get; set; }
    public string ChaveHash { get; set; } = string.Empty;
    public long TelegramChatId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
