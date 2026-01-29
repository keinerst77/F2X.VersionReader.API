using Microsoft.AspNetCore.Mvc;
using F2X.VersionReader.API.Services;

namespace F2X.VersionReader.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PowerShellRemoteTestController : ControllerBase
    {
        private readonly PowerShellRemoteValidationService _validationService;
        private readonly ILogger<PowerShellRemoteTestController> _logger;

        public PowerShellRemoteTestController(
            PowerShellRemoteValidationService validationService,
            ILogger<PowerShellRemoteTestController> logger)
        {
            _validationService = validationService;
            _logger = logger;
        }

        /// <summary>
        /// Ejecutar comando simple (hostname)
        /// </summary>
        [HttpPost("test-simple")]
        public async Task<ActionResult<RemoteCommandResult>> TestSimple(
            [FromBody] RemoteTestRequest request)
        {
            _logger.LogInformation("Iniciando prueba simple para: {IP}", request.IpEquipo);

            var resultado = await _validationService.TestComandoSimple(
                request.IpEquipo,
                request.Usuario,
                request.Password
            );

            if (resultado.Success)
            {
                return Ok(resultado);
            }
            else
            {
                return BadRequest(resultado);
            }
        }

        /// <summary>
        /// Listar archivos .exe
        /// </summary>
        [HttpPost("test-list-files")]
        public async Task<ActionResult<RemoteCommandResult>> TestListFiles(
            [FromBody] RemoteListFilesRequest request)
        {
            var resultado = await _validationService.TestListarArchivos(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.RutaRemota
            );

            return Ok(resultado);
        }

        /// <summary>
        /// Script (info del sistema)
        /// </summary>
        [HttpPost("test-script")]
        public async Task<ActionResult<RemoteCommandResult>> TestScript(
            [FromBody] RemoteTestRequest request)
        {
            var resultado = await _validationService.TestScriptComplejo(
                request.IpEquipo,
                request.Usuario,
                request.Password
            );

            return Ok(resultado);
        }
    }

    public class RemoteTestRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RemoteListFilesRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RutaRemota { get; set; } = string.Empty;
    }
}