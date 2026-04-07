using PsnPriceTracker.Helpers;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Services
{
    public class MonitoramentoService : IMonitoramentoService
    {
        private readonly IPsnIntegrationService _psnService;
        private readonly ITelegramBotApiService _telegramService;

        public MonitoramentoService(IPsnIntegrationService psnService, ITelegramBotApiService telegramService)
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
                string mensagem = $"🚨 *PSN PRICE ALERT!*\n\n" +
                                $"🎮 *Game:* {MarkdownSanitizer.Escape(dadosPsn.NomeDoJogo)}\n" +
                                $"💰 *Current Price:* R$ {dadosPsn.PrecoAtual}\n" +
                                $"🎯 *Your Target:* R$ {request.PrecoAlvo}\n\n" +
                                $"🛒 [Click here to buy]({request.UrlDoJogo})"; await _telegramService.SendMessageAsync(chatId, mensagem);

                return "Target price reached! Notification sent.";
            }

            return $"The current price (R$ {dadosPsn.PrecoAtual}) is still above your target.";
        }
    }
}
