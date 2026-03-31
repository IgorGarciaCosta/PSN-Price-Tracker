using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces
{
    public interface IPsnIntegrationService
    {
        /// <summary>
        /// Scrapes the PSN Store page for the given game URL and returns the game name and current price.
        /// </summary>
        Task<PrecoPsnDTO> GetCurrentPriceAsync(string gameUrl);

        /// <summary>
        /// Searches the PSN Store by game name and returns a list of matching results.
        /// </summary>
        Task<List<BuscaResultadoDTO>> BuscarJogosPorNomeAsync(string nomeDoJogo);
    }
}
