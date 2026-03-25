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

        [HttpPost("testar")]
        public async Task<IActionResult> TestarAlerta([FromBody] AlertaRequestDTO request)
        {
            //pass the hard work to the service layer
            var resultado = await _monitoramentoService.ProcessarAlertaAsync(request);

            return Ok(new { Mensagem = resultado });
        }
    }
}
