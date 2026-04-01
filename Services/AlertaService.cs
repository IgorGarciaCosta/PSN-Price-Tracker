using Microsoft.EntityFrameworkCore;
using PsnPriceTracker.Data;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class AlertaService : IAlertaService
{
    private readonly AppDbContext _context;

    public AlertaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AlertaEntity> CriarAlertaAsync(long chatId, string nomeDoJogo, string urlDoJogo, decimal precoAlvo)
    {
        var existente = await _context.Alertas
            .FirstOrDefaultAsync(a => a.TelegramChatId == chatId && a.UrlDoJogo == urlDoJogo && a.Ativo);

        if (existente is not null)
        {
            existente.PrecoAlvo = precoAlvo;
            await _context.SaveChangesAsync();
            return existente;
        }

        var alerta = new AlertaEntity
        {
            TelegramChatId = chatId,
            NomeDoJogo = nomeDoJogo,
            UrlDoJogo = urlDoJogo,
            PrecoAlvo = precoAlvo
        };

        _context.Alertas.Add(alerta);
        await _context.SaveChangesAsync();
        return alerta;
    }

    public async Task<List<AlertaEntity>> ObterAlertasAtivosAsync()
    {
        return await _context.Alertas
            .Where(a => a.Ativo)
            .ToListAsync();
    }

    public async Task<List<AlertaEntity>> ObterAlertasPorChatIdAsync(long chatId)
    {
        return await _context.Alertas
            .Where(a => a.TelegramChatId == chatId && a.Ativo)
            .ToListAsync();
    }

    public async Task DesativarAlertaAsync(int alertaId, long chatId)
    {
        var alerta = await _context.Alertas.FindAsync(alertaId);
        if (alerta is null || alerta.TelegramChatId != chatId)
            return;

        alerta.Ativo = false;
        await _context.SaveChangesAsync();
    }

    public async Task MarcarComoNotificadoAsync(int alertaId)
    {
        var alerta = await _context.Alertas.FindAsync(alertaId);
        if (alerta is null)
            return;

        alerta.Ativo = false;
        alerta.NotificadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
