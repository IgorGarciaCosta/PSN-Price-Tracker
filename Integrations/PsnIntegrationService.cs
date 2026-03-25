using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Integrations
{
    public class PsnIntegrationService : IPsnIntegrationService
    {
        public async Task<PrecoPsnDTO> ObterPrecoAtualAsync(string urlDoJogo)
        {
            // TODO: future: webscraping ou API oficial da PSN para obter o preço atual do jogo a partir da URL fornecida.
            //currently mock data for testing purposes
            return await Task.FromResult(new PrecoPsnDTO
            {
                NomeDoJogo = "The Last of Us Part II (Mock)",
                PrecoAtual = 149.90m
            });
        }
    }
}
