using Microsoft.AspNetCore.Mvc;
using F2X.VersionReader.API.Models;
using F2X.VersionReader.API.Services;
using System.Diagnostics;

namespace F2X.VersionReader.API.Controllers
{
    /// <summary>
    /// Controlador para comparación inteligente de carpetas
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ComparisonController : ControllerBase
    {
        private readonly FileVersionService _fileVersionService;
        private readonly ILogger<ComparisonController> _logger;

        public ComparisonController(
            FileVersionService fileVersionService,
            ILogger<ComparisonController> logger)
        {
            _fileVersionService = fileVersionService;
            _logger = logger;
        }

        /// <summary>
        /// Compara solo las carpetas que coinciden entre Versión Actual y Futura
        /// </summary>
        [HttpPost("compare-smart")]
        public async Task<ActionResult<SmartComparisonResponse>> CompareSmartAsync(
            [FromBody] SmartComparisonRequest request)
        {
            _logger.LogInformation("🔍 Iniciando comparación inteligente");
            _logger.LogInformation("   Actual:  {Actual}", request.RutaVersionActual);
            _logger.LogInformation("   Futura:  {Futura}", request.RutaVersionFutura);

            var response = new SmartComparisonResponse();

            try
            {
                // Obtener carpetas que coinciden
                var coincidencias = _fileVersionService.ObtenerCarpetasCoincidentes(
                    request.RutaVersionActual,
                    request.RutaVersionFutura
                );

                _logger.LogInformation("✅ {Count} carpetas coincidentes encontradas", coincidencias.Count);

                if (coincidencias.Count == 0)
                {
                    response.Success = false;
                    response.Message = "No se encontraron carpetas coincidentes entre ambas versiones";
                    return BadRequest(response);
                }

                // Escanear Versión Actual (solo carpetas coincidentes)
                var scanActual = await _fileVersionService.ScanDirectoryAsync(new ScanRequest
                {
                    Directory = request.RutaVersionActual,
                    CarpetasEspecificas = coincidencias.Select(c => c.carpetaActual).ToList(),
                    IncludeSubdirectories = true,
                    SearchPattern = "*.exe"
                });

                // Escanear Versión Futura (solo carpetas coincidentes)
                var scanFutura = await _fileVersionService.ScanDirectoryAsync(new ScanRequest
                {
                    Directory = request.RutaVersionFutura,
                    CarpetasEspecificas = coincidencias.Select(c => c.carpetaFutura).ToList(),
                    IncludeSubdirectories = true,
                    SearchPattern = "*.exe"
                });

                response.Success = true;
                response.ArchivosVersionActual = scanActual.Files;
                response.ArchivosVersionFutura = scanFutura.Files;
                response.CarpetasCoincidentes = coincidencias.Count;
                response.Message = $"Comparación completada. {coincidencias.Count} carpeta(s) coincidentes.";

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en comparación inteligente");
                response.Success = false;
                response.Message = "Error al realizar la comparación";
                return StatusCode(500, response);
            }
        }
    }

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
        /// Escanea un directorio local y retorna información de todos los archivos .exe
        /// </summary>
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
        /// Escanea un directorio femoto usando PowerShell Remoting
        /// </summary>
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
                    "/api/versionscanner/health (GET) - Health check",
                    "/api/comparison/compare-smart (POST) - Comparación inteligente"
                }
            });
        }
    }
}