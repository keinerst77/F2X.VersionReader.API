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
        /// Registrar evento de conexión en el equipo remoto
        /// </summary>
        [HttpPost("log-connection-event")]
        public async Task<IActionResult> LogConnectionEvent([FromBody] RemoteConnectionRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Intentando registrar Event Log en equipo remoto: {IP}", request.IpEquipo);

                // Script simplificado de PowerShell para escribir en Event Log
                string script = $@"
                try {{
                    $securePassword = ConvertTo-SecureString '{request.Password}' -AsPlainText -Force
                    $credential = New-Object System.Management.Automation.PSCredential('{request.Usuario}', $securePassword)
    
                    $result = Invoke-Command -ComputerName {request.IpEquipo} -Credential $credential -ScriptBlock {{
                        try {{
                            $hostname = $env:COMPUTERNAME
                            $loginTime = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
                            $remoteUser = $env:USERNAME
            
                            # Usar fuente genérica que siempre existe
                            $sourceName = 'Application'
            
                            # Mensaje del evento
                            $message = @'
                ╔═══════════════════════════════════════════════════════════╗
                ║          F2X - CONEXIÓN REMOTA ESTABLECIDA               ║
                ╚═══════════════════════════════════════════════════════════╝

                📅 Fecha y Hora: $loginTime
                🖥️  Equipo Local: $hostname
                👤 Usuario Conectado: $remoteUser
                🔐 Usuario Remoto: {request.Usuario}
                📡 IP Origen: {request.IpEquipo}
                🎯 Propósito: Escaneo de versiones de archivos

                ⚙️  Acción: Validación de conexión remota
                📋 Aplicación: F2X FileComparator v2.1.0

                ℹ️  Esta conexión fue establecida para realizar un escaneo
                   de archivos .exe y comparación de versiones.

                ═══════════════════════════════════════════════════════════
                '@
            
                            # Escribir en el Event Log
                            Write-EventLog -LogName Application -Source $sourceName -EventId 1001 -EntryType Information -Message $message -ErrorAction Stop
            
                            # Retornar confirmación
                            return @{{
                                Success = $true
                                Hostname = $hostname
                                LogTime = $loginTime
                                SourceUsed = $sourceName
                                Message = 'Event log entry created successfully'
                            }}
                        }} catch {{
                            # Si falla, retornar error pero sin romper
                            return @{{
                                Success = $false
                                Hostname = $env:COMPUTERNAME
                                Error = $_.Exception.Message
                            }}
                        }}
                    }} -ErrorAction Stop
    
                    return $result
                }} catch {{
                    # Error en la invocación remota
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
                        try
                        {
                            return ps.Invoke();
                        }
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

                        return Ok(new
                        {
                            success = false,
                            errorMessage = errorMessage,
                            message = "No se pudo registrar el evento, pero la conexión fue exitosa"
                        });
                    }

                    var result = results.FirstOrDefault();
                    if (result != null)
                    {
                        var resultObj = result.BaseObject as Hashtable;
                        if (resultObj != null)
                        {
                            var success = resultObj["Success"] as bool? ?? false;

                            if (success)
                            {
                                _logger.LogInformation("✅ Event Log registrado exitosamente");

                                return Ok(new
                                {
                                    success = true,
                                    message = "Event log registrado exitosamente en el equipo remoto",
                                    hostname = resultObj["Hostname"]?.ToString(),
                                    logTime = resultObj["LogTime"]?.ToString(),
                                    sourceUsed = resultObj["SourceUsed"]?.ToString()
                                });
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ No se pudo registrar Event Log: {Error}", resultObj["Error"]?.ToString());

                                return Ok(new
                                {
                                    success = false,
                                    message = "No se pudo registrar el evento, pero la conexión fue exitosa",
                                    hostname = resultObj["Hostname"]?.ToString(),
                                    error = resultObj["Error"]?.ToString()
                                });
                            }
                        }
                    }

                    return Ok(new
                    {
                        success = false,
                        message = "No se recibió respuesta del equipo remoto",
                        errorMessage = "Sin respuesta"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error al intentar registrar Event Log (no crítico)");

                // Retornar 200 OK con success=false
                return Ok(new
                {
                    success = false,
                    errorMessage = ex.Message,
                    message = "La conexión fue exitosa, pero no se pudo registrar el evento"
                });
            }
        }

        /// <summary>
        /// Validar si un directorio existe en el equipo remoto
        /// </summary>
        [HttpPost("validate-directory")]
        public async Task<ActionResult<RemoteCommandResult>> ValidateDirectory(
            [FromBody] RemoteDirectoryValidationRequest request)
        {
            _logger.LogInformation("Validando directorio remoto: {Path} en {IP}",
                request.RutaDirectorio, request.IpEquipo);

            var resultado = await _validationService.ValidarDirectorioRemoto(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.RutaDirectorio
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
            _logger.LogInformation("Buscando carpeta '{Name}' en {IP}",
                request.NombreCarpeta, request.IpEquipo);

            var resultado = await _validationService.BuscarDirectorioPorNombre(
                request.IpEquipo,
                request.Usuario,
                request.Password,
                request.NombreCarpeta
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
    }

    // ═══════════════════════════════════════════════════
    // MODELOS DE REQUEST
    // ═══════════════════════════════════════════════════

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