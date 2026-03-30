using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PsnPriceTracker.Data;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly AppDbContext _dbContext;

    public ApiKeyService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GerarApiKeyAsync(long telegramChatId)
    {
        var chave = GenerateSecureKey();

        var entity = new ApiKeyEntity
        {
            Chave = chave,
            TelegramChatId = telegramChatId,
            CriadoEm = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync();

        return chave;
    }

    public async Task<bool> ValidarApiKeyAsync(string chave)
    {
        return await _dbContext.ApiKeys.AnyAsync(k => k.Chave == chave);
    }

    private static string GenerateSecureKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
