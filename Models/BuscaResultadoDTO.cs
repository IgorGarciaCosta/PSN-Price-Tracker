namespace PsnPriceTracker.Models
{
    public class BuscaResultadoDTO
    {
        public string NomeDoJogo { get; set; } = string.Empty;
        public string UrlDoJogo { get; set; } = string.Empty;
        public string? Plataforma { get; set; }
        public string? ImagemUrl { get; set; }
        public string? PrecoAtual { get; set; }
    }
}
