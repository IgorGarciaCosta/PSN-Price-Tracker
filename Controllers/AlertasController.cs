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

        public AlertasController(IMonitoramentoService monitoramentoService)
        {
            _monitoramentoService = monitoramentoService;
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
            //pass the hard work to the service layer
            var resultado = await _monitoramentoService.ProcessarAlertaAsync(request);

            return Ok(new { Mensagem = resultado });
        }
    }
}
