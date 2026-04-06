using Microsoft.AspNetCore.Mvc;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertasController : ControllerBase
    {
        private readonly IMonitoramentoService _monitoramentoService;
        private readonly IPsnIntegrationService _psnService;

        public AlertasController(IMonitoramentoService monitoramentoService, IPsnIntegrationService psnService)
        {
            _monitoramentoService = monitoramentoService;
            _psnService = psnService;
        }

        [HttpGet("jogos-mock")]
        public IActionResult ObterJogosMock()
        {
            var jogos = new List<PrecoPsnDTO>
            {
                new PrecoPsnDTO { NomeDoJogo = "God of War Ragnarök", PrecoAtual = 149.90m },
                new PrecoPsnDTO { NomeDoJogo = "Spider-Man 2", PrecoAtual = 199.90m },
                new PrecoPsnDTO { NomeDoJogo = "The Last of Us Part II", PrecoAtual = 99.90m }
            };

            return Ok(jogos);
        }

        [HttpPost("testar")]
        public async Task<IActionResult> TestarAlerta([FromBody] AlertaRequestDTO request)
        {
            var chatId = (long)HttpContext.Items["TelegramChatId"]!;

            //pass the hard work to the service layer
            var resultado = await _monitoramentoService.ProcessarAlertaAsync(request, chatId);

            return Ok(new { Mensagem = resultado });
        }

        [HttpGet("buscar-jogo")]
        public async Task<IActionResult> BuscarJogo([FromQuery] string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return BadRequest(new { Mensagem = "O parâmetro 'nome' é obrigatório." });

            var resultados = await _psnService.BuscarJogosPorNomeAsync(nome);

            if (resultados.Count == 0)
                return NotFound(new { Mensagem = $"Nenhum jogo encontrado para '{nome}'." });

            return Ok(resultados);
        }
    }
}
