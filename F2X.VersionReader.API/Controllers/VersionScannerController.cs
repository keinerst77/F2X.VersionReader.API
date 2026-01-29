using Microsoft.AspNetCore.Mvc;
using F2X.VersionReader.API.Models;
using F2X.VersionReader.API.Services;

namespace F2X.VersionReader.API.Controllers
{
    /// <summary>
    /// Controlador para escanear directorios y obtener versiones de archivos .exe
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class VersionScannerController : ControllerBase
    {
        private readonly FileVersionService _fileVersionService;
        private readonly ILogger<VersionScannerController> _logger;

        public VersionScannerController(
            FileVersionService fileVersionService,
            ILogger<VersionScannerController> logger)
        {
            _fileVersionService = fileVersionService;
            _logger = logger;
        }

        /// <summary>
        /// Escanea un directorio LOCAL y retorna información de todos los archivos .exe
        /// </summary>
        /// <param name="request">Información del directorio a escanear</param>
        /// <returns>Lista de archivos .exe con sus versiones</returns>
        /// <response code="200">Escaneo exitoso</response>
        /// <response code="400">Petición inválida</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpPost("scan")]
        [ProducesResponseType(typeof(ScanResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ScanResponse>> ScanDirectory([FromBody] ScanRequest request)
        {
            _logger.LogInformation("Recibida petición de escaneo LOCAL para: {Directory}", request.Directory);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _fileVersionService.ScanDirectoryAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Escanea un directorio REMOTO usando PowerShell Remoting
        /// </summary>
        /// <param name="request">Información del equipo y directorio remoto a escanear</param>
        /// <returns>Lista de archivos .exe con sus versiones del equipo remoto</returns>
        /// <response code="200">Escaneo remoto exitoso</response>
        /// <response code="400">Petición inválida o error de conexión</response>
        [HttpPost("scan-remote")]
        [ProducesResponseType(typeof(ScanResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ScanResponse>> ScanDirectoryRemote([FromBody] RemoteScanRequest request)
        {
            _logger.LogInformation("Recibida petición de escaneo REMOTO para: {IP}:{Ruta}",
                request.IpEquipo, request.RutaRemota);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _fileVersionService.ScanDirectoryRemoteAsync(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.RutaRemota,
                request.IncludeSubdirectories,
                request.SearchPattern
            );

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Verificar que el servicio está funcionando
        /// </summary>
        /// <returns>Mensaje de estado</returns>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Service = "F2X Version Reader API",
                Version = "1.0.0",
                Timestamp = DateTime.UtcNow,
                Endpoints = new[]
                {
                    "/api/versionscanner/scan (POST) - Escaneo local",
                    "/api/versionscanner/scan-remote (POST) - Escaneo remoto",
                    "/api/versionscanner/health (GET) - Health check"
                }
            });
        }
    }
}