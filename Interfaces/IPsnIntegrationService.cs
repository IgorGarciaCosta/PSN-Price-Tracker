using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces
{
    public interface IPsnIntegrationService
    {
        Task<PrecoPsnDTO> ObterPrecoAtualAsync(string urlDoJogo);
    }
}
