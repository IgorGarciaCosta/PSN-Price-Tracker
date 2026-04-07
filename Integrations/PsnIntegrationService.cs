using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Integrations
{
    public class PsnIntegrationService : IPsnIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PsnIntegrationService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public PsnIntegrationService(HttpClient httpClient, ILogger<PsnIntegrationService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
        /// <summary>
        /// Scrapes the PSN Store page for the given game URL and returns the game name and current price.
        /// </summary>
        public async Task<PrecoPsnDTO> GetCurrentPriceAsync(string gameUrl)
        {
            var cacheKey = $"psn_price:{gameUrl}";

            if (_cache.TryGetValue(cacheKey, out PrecoPsnDTO? cached) && cached is not null)
            {
                _logger.LogDebug("Cache hit para {Url}", gameUrl);
                return cached;
            }

            try
            {
                var html = await FetchHtmlAsync(gameUrl);
                var htmlDoc = ParseHtml(html);

                string gameName = ExtractGameName(htmlDoc);
                decimal currentPrice = ExtractPrice(htmlDoc);

                var resultado = new PrecoPsnDTO
                {
                    NomeDoJogo = gameName,
                    PrecoAtual = currentPrice
                };

                _cache.Set(cacheKey, resultado, CacheDuration);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process URL");
                throw;
            }
        }

        /// <summary>
        /// Searches the PSN Store API by game name and returns up to 5 matching games.
        /// Uses the internal Chihiro/tumbler API which provides fuzzy matching.
        /// </summary>
        public async Task<List<BuscaResultadoDTO>> BuscarJogosPorNomeAsync(string nomeDoJogo)
        {
            var query = Uri.EscapeDataString(nomeDoJogo);
            var searchUrl = $"https://store.playstation.com/store/api/chihiro/00_09_000/tumbler/BR/pt/999/{query}?suggested_size=5&mode=mixed";

            var response = await _httpClient.GetAsync(searchUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var resultados = new List<BuscaResultadoDTO>();

            if (!doc.RootElement.TryGetProperty("links", out var links))
                return resultados;

            foreach (var link in links.EnumerateArray())
            {
                var topCategory = link.TryGetProperty("top_category", out var cat) ? cat.GetString() : null;
                if (topCategory != "downloadable_game")
                    continue;

                var name = link.TryGetProperty("name", out var n) ? n.GetString() : null;
                var id = link.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id))
                    continue;

                var platforms = link.TryGetProperty("playable_platform", out var plats)
                    ? string.Join(", ", plats.EnumerateArray().Select(p => p.GetString()))
                    : null;

                var imageUrl = link.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0
                    ? imgs[0].TryGetProperty("url", out var imgUrl) ? imgUrl.GetString() : null
                    : null;

                var price = link.TryGetProperty("default_sku", out var sku)
                    && sku.TryGetProperty("display_price", out var dp)
                    ? dp.GetString()
                    : null;

                resultados.Add(new BuscaResultadoDTO
                {
                    NomeDoJogo = name,
                    UrlDoJogo = $"https://store.playstation.com/pt-br/product/{id}",
                    Plataforma = platforms,
                    ImagemUrl = imageUrl,
                    PrecoAtual = price
                });

                if (resultados.Count >= 5)
                    break;
            }

            await EnrichMissingPricesAsync(resultados);

            return resultados;
        }

        /// <summary>
        /// For each result with a null price, attempts to fetch the price from the
        /// Chihiro Container (detail) API in parallel.
        /// </summary>
        private async Task EnrichMissingPricesAsync(List<BuscaResultadoDTO> resultados)
        {
            var semPreco = resultados.Where(r => string.IsNullOrEmpty(r.PrecoAtual)).ToList();
            if (semPreco.Count == 0) return;

            var tasks = semPreco.Select(async resultado =>
            {
                var productId = resultado.UrlDoJogo.Split("/product/").LastOrDefault();
                if (string.IsNullOrEmpty(productId)) return;

                var price = await FetchPriceFromContainerAsync(productId);
                if (!string.IsNullOrEmpty(price))
                    resultado.PrecoAtual = price;
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Fetches the price from the Chihiro Container (detail) API for a single product.
        /// Returns the display_price string or null if unavailable.
        /// </summary>
        private async Task<string?> FetchPriceFromContainerAsync(string productId)
        {
            var cacheKey = $"psn_container_price:{productId}";

            if (_cache.TryGetValue(cacheKey, out string? cached))
                return cached;

            try
            {
                var url = $"https://store.playstation.com/store/api/chihiro/00_09_000/container/BR/pt/999/{Uri.EscapeDataString(productId)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                string? price = null;

                if (doc.RootElement.TryGetProperty("default_sku", out var sku)
                    && sku.TryGetProperty("display_price", out var dp))
                {
                    price = dp.GetString();
                }

                _cache.Set(cacheKey, price, CacheDuration);
                return price;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback: falha ao buscar preço via Container API para {ProductId}", productId);
                return null;
            }
        }

        /// <summary>
        /// Sends a GET request to the given URL and returns the raw HTML content.
        /// </summary>
        private async Task<string> FetchHtmlAsync(string url)
        {
            ValidateUrl(url);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "store.playstation.com"
        };

        /// <summary>
        /// Validates that the URL uses HTTPS and targets an allowed PSN Store domain.
        /// Prevents SSRF attacks by blocking requests to internal/unauthorized hosts.
        /// </summary>
        private static void ValidateUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("URL inválida.");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Apenas URLs HTTPS são permitidas.");

            if (!AllowedHosts.Contains(uri.Host))
                throw new ArgumentException($"Domínio não permitido: {uri.Host}. Apenas store.playstation.com é aceito.");
        }

        /// <summary>
        /// Loads raw HTML into an HtmlAgilityPack document for XPath querying.
        /// </summary>
        private HtmlDocument ParseHtml(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            return htmlDoc;
        }

        /// <summary>
        /// Extracts the game name from the parsed HTML using PSN's data-qa attributes,
        /// falling back to the first h1 element if the primary selector is not found.
        /// </summary>
        private string ExtractGameName(HtmlDocument htmlDoc)
        {
            var nameNode = htmlDoc.DocumentNode.SelectSingleNode("//*[@data-qa='mfe-game-title#name']")
                ?? htmlDoc.DocumentNode.SelectSingleNode("//h1");

            if (nameNode == null)
                throw new ApplicationException("Não foi possível encontrar os dados do jogo na página. Verifique a URL e tente novamente.");

            return nameNode.InnerText.Trim();
        }

        /// <summary>
        /// Extracts the purchase price by iterating through available offers (offer0, offer1, etc.).
        /// PS Plus games show "Incluído" in offer0, so the method skips non-numeric values
        /// and falls back to the original price if no numeric final price is found.
        /// </summary>
        private decimal ExtractPrice(HtmlDocument htmlDoc)
        {
            for (int i = 0; i < 5; i++)
            {
                var priceNode = htmlDoc.DocumentNode.SelectSingleNode($"//*[@data-qa='mfeCtaMain#offer{i}#finalPrice']");
                if (priceNode == null) break;

                if (TryParsePrice(priceNode.InnerText, out decimal price))
                    return price;
            }

            var originalNode = htmlDoc.DocumentNode.SelectSingleNode("//*[@data-qa='mfeCtaMain#offer0#originalPrice']");
            if (originalNode != null && TryParsePrice(originalNode.InnerText, out decimal originalPrice))
                return originalPrice;

            throw new ApplicationException("Não foi possível encontrar um preço numérico na página. O jogo pode estar disponível apenas via PS Plus.");
        }

        /// <summary>
        /// Removes currency symbols and whitespace from the price text
        /// and attempts to parse it as a decimal using Brazilian (pt-BR) formatting.
        /// </summary>
        private bool TryParsePrice(string priceText, out decimal price)
        {
            var cleanText = priceText.Trim()
                .Replace("&nbsp;", "")
                .Replace("R$", "")
                .Trim();

            return decimal.TryParse(cleanText, new CultureInfo("pt-BR"), out price);
        }
    }
}
