using Microsoft.AspNetCore.Mvc;
using PsnPriceTracker.Interfaces;
using PsnPriceTracker.Models;

namespace PsnPriceTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Alertas")]
    [Produces("application/json")]
    public class AlertasController : ControllerBase
    {
        private readonly IMonitoramentoService _monitoramentoService;
        private readonly IPsnIntegrationService _psnService;

        public AlertasController(IMonitoramentoService monitoramentoService, IPsnIntegrationService psnService)
        {
            _monitoramentoService = monitoramentoService;
            _psnService = psnService;
        }

        /// <summary>
        /// Busca jogos na PlayStation Store pelo nome.
        /// </summary>
        /// <param name="nome">Nome do jogo a ser buscado.</param>
        [HttpGet("buscar-jogo")]
        [ProducesResponseType(typeof(List<BuscaResultadoDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BuscarJogo([FromQuery] string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return BadRequest(new { Message = "The 'nome' parameter is required." });

            var resultados = await _psnService.BuscarJogosPorNomeAsync(nome);

            if (resultados.Count == 0)
                return NotFound(new { Message = $"No games found for '{nome}'." });

            return Ok(resultados);
        }
    }
}
