using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services
{
    public class MonitoramentoService : IMonitoramentoService
    {
        private readonly IPsnIntegrationService _psnService;
        private readonly ITelegramIntegrationService _telegramService;

        public MonitoramentoService(IPsnIntegrationService psnService, ITelegramIntegrationService telegramService)
        {
            _psnService = psnService;
            _telegramService = telegramService;
        }

        public async Task<string> ProcessarAlertaAsync(AlertaRequestDTO request, long chatId)
        {
            //get price on PSN using the provided URL
            var dadosPsn = await _psnService.GetCurrentPriceAsync(request.UrlDoJogo);

            //compare the current price with the target price provided by the user
            if (dadosPsn.PrecoAtual <= request.PrecoAlvo)
            {
                //formats the message and sends it to the Telegram service
                string mensagem = $"🚨 *ALERTA DE PREÇO PSN!*\n\n" +
                                $"🎮 *Jogo:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n" +
                                $"💰 *Preço Atual:* R$ {dadosPsn.PrecoAtual}\n" +
                                $"🎯 *Seu Alvo:* R$ {request.PrecoAlvo}\n\n" +
                                $"🛒 [Clique aqui para comprar]({request.UrlDoJogo})"; await _telegramService.SendMessageAsync(chatId, mensagem);

                return "Preço atingido! Notificação enviada.";
            }

            return $"O preço atual (R$ {dadosPsn.PrecoAtual}) ainda está acima do seu alvo.";
        }
    }
}
