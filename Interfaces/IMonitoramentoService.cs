using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces
{
    public interface IMonitoramentoService
    {
        Task<string> ProcessarAlertaAsync(AlertaRequestDTO request, long chatId);
    }
}
