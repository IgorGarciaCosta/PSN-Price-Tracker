using HtmlAgilityPack;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;
using System.Globalization;

namespace PsnPriceTracker.Integrations
{
    public class PsnIntegrationService : IPsnIntegrationService
    {
        private readonly HttpClient _httpClient;

        public PsnIntegrationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
        public async Task<PrecoPsnDTO> ObterPrecoAtualAsync(string urlDoJogo)
        {
            try
            {
                //downloads the HTML content of the provided PSN game URL
                var response = await _httpClient.GetAsync(urlDoJogo);
                response.EnsureSuccessStatusCode();// generates an exception if the status code is not 2xx

                var html = await response.Content.ReadAsStringAsync();

                //loads html content into HtmlAgilityPack for parsing
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var nameNode = htmlDoc.DocumentNode.SelectSingleNode("//*[@data-qa='mfe-game-title#name']")
                ?? htmlDoc.DocumentNode.SelectSingleNode("//h1"); // fallback for different page structure

                if (nameNode == null)
                {
                    throw new ApplicationException("Não foi possível encontrar os dados do jogo na página. Verifique a URL e tente novamente.");
                }

                string nomeDoJogo = nameNode.InnerText.Trim();

                // Tenta encontrar o preço real percorrendo as ofertas disponíveis (offer0, offer1, etc.)
                // Jogos inclusos no PS Plus mostram "Incluído" no offer0, mas o preço de compra fica no offer1
                decimal precoAtual = -1;
                for (int i = 0; i < 5; i++)
                {
                    var precoNode = htmlDoc.DocumentNode.SelectSingleNode($"//*[@data-qa='mfeCtaMain#offer{i}#finalPrice']");
                    if (precoNode == null) break;

                    string precoTexto = precoNode.InnerText.Trim()
                        .Replace("&nbsp;", "").Replace("R$", "").Trim();

                    if (decimal.TryParse(precoTexto, new CultureInfo("pt-BR"), out decimal preco))
                    {
                        precoAtual = preco;
                        break;
                    }
                }

                // Fallback: tenta o preço original (sem desconto) do offer0
                if (precoAtual < 0)
                {
                    var originalNode = htmlDoc.DocumentNode.SelectSingleNode("//*[@data-qa='mfeCtaMain#offer0#originalPrice']");
                    if (originalNode != null)
                    {
                        string textoOriginal = originalNode.InnerText.Trim()
                            .Replace("&nbsp;", "").Replace("R$", "").Trim();
                        decimal.TryParse(textoOriginal, new CultureInfo("pt-BR"), out precoAtual);
                    }
                }

                if (precoAtual < 0)
                {
                    throw new ApplicationException("Não foi possível encontrar um preço numérico na página. O jogo pode estar disponível apenas via PS Plus.");
                }

                return new PrecoPsnDTO
                {
                    NomeDoJogo = nomeDoJogo,
                    PrecoAtual = precoAtual
                };
            }
            catch (Exception ex)
            {
                // Em um projeto real, usaríamos um ILogger aqui.
                Console.WriteLine($"[ERRO SCRAPING] Falha ao processar a URL: {ex.Message}");
                throw;
            }

        }
    }
}
