namespace PsnPriceTracker.Models
{
    public class AlertaRequestDTO
    {
        public string UrlDoJogo { get; set; } = string.Empty;
        public decimal PrecoAlvo { get; set; }
    }
}
