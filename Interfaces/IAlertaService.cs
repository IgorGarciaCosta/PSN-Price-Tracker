using PsnPriceTracker.Models;

namespace PsnPriceTracker.Interfaces;

public interface IAlertaService
{
    Task<AlertaEntity> CriarAlertaAsync(long chatId, string nomeDoJogo, string urlDoJogo, decimal precoAlvo);
    Task<List<AlertaEntity>> ObterAlertasAtivosAsync();
    Task<List<AlertaEntity>> ObterAlertasPorChatIdAsync(long chatId);
    Task DesativarAlertaAsync(int alertaId);
    Task MarcarComoNotificadoAsync(int alertaId);
}
