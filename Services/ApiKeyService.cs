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
        var jaExiste = await _dbContext.ApiKeys
            .AnyAsync(k => k.TelegramChatId == telegramChatId);

        if (jaExiste)
            return string.Empty;

        var chaveRaw = GenerateSecureKey();
        var hash = HashKey(chaveRaw);

        var entity = new ApiKeyEntity
        {
            ChaveHash = hash,
            TelegramChatId = telegramChatId,
            CriadoEm = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync();

        return chaveRaw;
    }

    public async Task<bool> ValidarApiKeyAsync(string chave)
    {
        var hash = HashKey(chave);
        return await _dbContext.ApiKeys.AnyAsync(k => k.ChaveHash == hash);
    }

    public async Task<long?> ObterChatIdPorApiKeyAsync(string chave)
    {
        var hash = HashKey(chave);
        return await _dbContext.ApiKeys
            .Where(k => k.ChaveHash == hash)
            .Select(k => (long?)k.TelegramChatId)
            .FirstOrDefaultAsync();
    }

    private static string GenerateSecureKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashKey(string key)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(bytes);
    }
}
