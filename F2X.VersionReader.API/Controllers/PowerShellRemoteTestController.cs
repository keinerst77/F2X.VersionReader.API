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
        /// Script (info del sistema)
        /// </summary>
        [HttpPost("test-script")]
        [HttpPost("log-connection-event")]
        public IActionResult LogConnectionEvent([FromBody] RemoteConnectionRequest request)
        {
            try
            {
                // Script de PowerShell para escribir en Event Log del equipo remoto
                string script = $@"
            $securePassword = ConvertTo-SecureString '{request.Password}' -AsPlainText -Force
            $credential = New-Object System.Management.Automation.PSCredential('{request.Usuario}', $securePassword)
            
            Invoke-Command -ComputerName {request.IpEquipo} -Credential $credential -ScriptBlock {{
                # Nombre de la fuente del evento
                $sourceName = 'F2X-FileComparator'
                
                # Verificar si la fuente existe, si no, crearla
                if (-not [System.Diagnostics.EventLog]::SourceExists($sourceName)) {{
                    try {{
                        New-EventLog -LogName Application -Source $sourceName
                        Write-EventLog -LogName Application -Source $sourceName -EventId 1000 -EntryType Information -Message 'F2X FileComparator source created successfully'
                    }} catch {{
                        # Si falla crear la fuente, usar una fuente genérica
                        $sourceName = 'Application'
                    }}
                }}
                
                # Obtener información del equipo local
                $hostname = $env:COMPUTERNAME
                $loginTime = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
                $remoteUser = $env:USERNAME
                
                # Mensaje del evento
                $message = @""
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
""@
                
                # Escribir en el Event Log
                Write-EventLog -LogName Application -Source $sourceName -EventId 1001 -EntryType Information -Message $message
                
                # Retornar confirmación
                return @{{
                    Success = $true
                    Hostname = $hostname
                    LogTime = $loginTime
                    Message = 'Event log entry created successfully'
                }}
            }}
        ";

                using (PowerShell ps = PowerShell.Create())
                {
                    ps.AddScript(script);
                    var results = ps.Invoke();

                    if (ps.HadErrors)
                    {
                        var errors = ps.Streams.Error.ReadAll();
                        return BadRequest(new
                        {
                            success = false,
                            errorMessage = string.Join("; ", errors.Select(e => e.ToString()))
                        });
                    }

                    var result = results.FirstOrDefault();
                    if (result != null)
                    {
                        var resultObj = result.BaseObject as Hashtable;
                        return Ok(new
                        {
                            success = true,
                            message = "Event log registrado exitosamente en el equipo remoto",
                            hostname = resultObj?["Hostname"]?.ToString(),
                            logTime = resultObj?["LogTime"]?.ToString()
                        });
                    }

                    return Ok(new { success = true, message = "Event log registrado" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    errorMessage = $"Error al registrar event log: {ex.Message}"
                });
            }
        }

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

    public class RemoteConnectionRequest
    {
        public string IpEquipo { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

}