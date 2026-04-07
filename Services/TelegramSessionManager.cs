using System.Collections.Concurrent;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services;

public class TelegramSessionManager
{
    private readonly ConcurrentDictionary<long, TimestampedEntry<PendingAlert>> _pendingAlerts = new();
    private readonly ConcurrentDictionary<long, TimestampedEntry<List<BuscaResultadoDTO>>> _searchResults = new();
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    public void SetPendingAlert(long chatId, string urlDoJogo, string nomeDoJogo)
    {
        _pendingAlerts[chatId] = new TimestampedEntry<PendingAlert>(
            new PendingAlert { UrlDoJogo = urlDoJogo, NomeDoJogo = nomeDoJogo });
    }

    public PendingAlert? TryGetPendingAlert(long chatId)
    {
        if (_pendingAlerts.TryGetValue(chatId, out var entry) && !entry.IsExpired(EntryTtl))
            return entry.Value;

        _pendingAlerts.TryRemove(chatId, out _);
        return null;
    }

    public void RemovePendingAlert(long chatId)
    {
        _pendingAlerts.TryRemove(chatId, out _);
    }

    public void SetSearchResults(long chatId, List<BuscaResultadoDTO> resultados)
    {
        _searchResults[chatId] = new TimestampedEntry<List<BuscaResultadoDTO>>(resultados);
    }

    public List<BuscaResultadoDTO>? TryRemoveSearchResults(long chatId)
    {
        if (_searchResults.TryRemove(chatId, out var entry) && !entry.IsExpired(EntryTtl))
            return entry.Value;

        return null;
    }

    public void ClearSession(long chatId)
    {
        _pendingAlerts.TryRemove(chatId, out _);
        _searchResults.TryRemove(chatId, out _);
    }

    public void CleanupExpired()
    {
        foreach (var key in _pendingAlerts.Keys)
        {
            if (_pendingAlerts.TryGetValue(key, out var pa) && pa.IsExpired(EntryTtl))
                _pendingAlerts.TryRemove(key, out _);
        }

        foreach (var key in _searchResults.Keys)
        {
            if (_searchResults.TryGetValue(key, out var sr) && sr.IsExpired(EntryTtl))
                _searchResults.TryRemove(key, out _);
        }
    }

    public class PendingAlert
    {
        public string UrlDoJogo { get; set; } = string.Empty;
        public string NomeDoJogo { get; set; } = string.Empty;
    }

    private sealed record TimestampedEntry<T>(T Value)
    {
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public bool IsExpired(TimeSpan ttl) => DateTime.UtcNow - CreatedAtUtc > ttl;
    }
}
