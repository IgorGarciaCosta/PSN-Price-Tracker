using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Integrations
{
    public class PsnIntegrationService : IPsnIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PsnIntegrationService> _logger;

        public PsnIntegrationService(HttpClient httpClient, ILogger<PsnIntegrationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
        /// <summary>
        /// Scrapes the PSN Store page for the given game URL and returns the game name and current price.
        /// </summary>
        public async Task<PrecoPsnDTO> GetCurrentPriceAsync(string gameUrl)
        {
            try
            {
                var html = await FetchHtmlAsync(gameUrl);
                var htmlDoc = ParseHtml(html);

                string gameName = ExtractGameName(htmlDoc);
                decimal currentPrice = ExtractPrice(htmlDoc);

                return new PrecoPsnDTO
                {
                    NomeDoJogo = gameName,
                    PrecoAtual = currentPrice
                };
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

            return resultados;
        }

        /// <summary>
        /// Sends a GET request to the given URL and returns the raw HTML content.
        /// </summary>
        private async Task<string> FetchHtmlAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
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
