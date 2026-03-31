namespace PsnPriceTracker.Models;

public class AlertaEntity
{
    public int Id { get; set; }
    public long TelegramChatId { get; set; }
    public string NomeDoJogo { get; set; } = string.Empty;
    public string UrlDoJogo { get; set; } = string.Empty;
    public decimal PrecoAlvo { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? NotificadoEm { get; set; }
}
