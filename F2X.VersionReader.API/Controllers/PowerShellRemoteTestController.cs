using F2X.VersionReader.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Management.Automation;

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
            if (string.IsNullOrWhiteSpace(request.IpEquipo) ||
                string.IsNullOrWhiteSpace(request.Usuario) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new RemoteCommandResult
                {
                    Success = false,
                    ErrorMessage = "IpEquipo, Usuario y Password son requeridos"
                });
            }

            _logger.LogInformation("Iniciando prueba simple para: {IP}", request.IpEquipo);

            var resultado = await _validationService.TestComandoSimple(
                request.IpEquipo,
                request.Usuario,
                request.Password
            );

            return Ok(resultado);
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
        /// Registrar evento de conexión en el equipo remoto
        /// </summary>
        [HttpPost("log-connection-event")]
        public async Task<IActionResult> LogConnectionEvent([FromBody] RemoteConnectionRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Intentando registrar Event Log en equipo remoto: {IP}", request.IpEquipo);

                string script = $@"
                try {{
                    $securePassword = ConvertTo-SecureString '{request.Password}' -AsPlainText -Force
                    $credential = New-Object System.Management.Automation.PSCredential('{request.Usuario}', $securePassword)
    
                    $result = Invoke-Command -ComputerName {request.IpEquipo} -Credential $credential -ScriptBlock {{
                        try {{
                            $hostname = $env:COMPUTERNAME
                            $loginTime = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
                            $remoteUser = $env:USERNAME
                            $sourceName = 'Application'
                            $message = ""F2X - CONEXION REMOTA - $loginTime - Usuario: {request.Usuario}""
                            Write-EventLog -LogName Application -Source $sourceName -EventId 1001 -EntryType Information -Message $message -ErrorAction Stop
                            return @{{
                                Success = $true
                                Hostname = $hostname
                                LogTime = $loginTime
                                SourceUsed = $sourceName
                                Message = 'Event log entry created successfully'
                            }}
                        }} catch {{
                            return @{{
                                Success = $false
                                Hostname = $env:COMPUTERNAME
                                Error = $_.Exception.Message
                            }}
                        }}
                    }} -ErrorAction Stop
    
                    return $result
                }} catch {{
                    return @{{
                        Success = $false
                        Error = $_.Exception.Message
                    }}
                }}
                ";

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.AddScript(script);

                    var results = await Task.Run(() =>
                    {
                        try { return ps.Invoke(); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("⚠️ Error al invocar PowerShell: {Error}", ex.Message);
                            return new System.Collections.ObjectModel.Collection<PSObject>();
                        }
                    });

                    if (ps.HadErrors)
                    {
                        var errors = ps.Streams.Error.ReadAll();
                        var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
                        _logger.LogWarning("⚠️ Error al registrar Event Log: {Error}", errorMessage);
                        return Ok(new { success = false, errorMessage, message = "No se pudo registrar el evento, pero la conexión fue exitosa" });
                    }

                    var result = results.FirstOrDefault();
                    if (result?.BaseObject is Hashtable resultObj)
                    {
                        var success = resultObj["Success"] as bool? ?? false;
                        if (success)
                        {
                            _logger.LogInformation("✅ Event Log registrado exitosamente");
                            return Ok(new { success = true, message = "Event log registrado", hostname = resultObj["Hostname"]?.ToString(), logTime = resultObj["LogTime"]?.ToString(), sourceUsed = resultObj["SourceUsed"]?.ToString() });
                        }
                        else
                        {
                            return Ok(new { success = false, message = "No se pudo registrar el evento, pero la conexión fue exitosa", hostname = resultObj["Hostname"]?.ToString(), error = resultObj["Error"]?.ToString() });
                        }
                    }

                    return Ok(new { success = false, message = "No se recibió respuesta del equipo remoto" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error al intentar registrar Event Log (no crítico)");
                return Ok(new { success = false, errorMessage = ex.Message, message = "La conexión fue exitosa, pero no se pudo registrar el evento" });
            }
        }

        /// <summary>
        /// Validar si un directorio existe en el equipo remoto
        /// </summary>
        [HttpPost("validate-directory")]
        public async Task<ActionResult<RemoteCommandResult>> ValidateDirectory(
            [FromBody] RemoteDirectoryValidationRequest request)
        {
            _logger.LogInformation("Validando directorio remoto: {Path} en {IP}", request.RutaDirectorio, request.IpEquipo);

            var resultado = await _validationService.ValidarDirectorioRemoto(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.RutaDirectorio
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


        /// <summary>
        /// Buscar directorio por nombre en ubicaciones comunes
        /// </summary>
        [HttpPost("search-directory-by-name")]
        public async Task<ActionResult<RemoteCommandResult>> SearchDirectoryByName(
            [FromBody] RemoteDirectorySearchRequest request)
        {
            _logger.LogInformation("Buscando carpeta '{Name}' en {IP}", request.NombreCarpeta, request.IpEquipo);

            var resultado = await _validationService.BuscarDirectorioPorNombre(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.NombreCarpeta
            );

            return Ok(resultado);
        }
    }


    // MODELOS DE REQUEST
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

    public class RemoteConnectionRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RemoteDirectoryValidationRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RutaDirectorio { get; set; } = string.Empty;
    }

    public class RemoteDirectorySearchRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCarpeta { get; set; } = string.Empty;
    }
}