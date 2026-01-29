using System.Management.Automation.Remoting;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace F2X.VersionReader.API.Services
{
    /// <summary>
    /// Servicio para validar ejecución remota de PowerShell
    /// </summary>
    public class PowerShellRemoteValidationService
    {
        private readonly ILogger<PowerShellRemoteValidationService> _logger;

        public PowerShellRemoteValidationService(ILogger<PowerShellRemoteValidationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ejecutar comando simple (hostname)
        /// </summary>
        public async Task<RemoteCommandResult> TestComandoSimple(
            string ipEquipo,
            string usuario,
            string password)
        {
            var resultado = new RemoteCommandResult
            {
                IpEquipo = ipEquipo,
                Comando = "hostname"
            };

            try
            {
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("🔍 PRUEBA 1: Comando Simple Remoto");
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("📍 Equipo: {IP}", ipEquipo);
                _logger.LogInformation("👤 Usuario: {Usuario}", usuario);
                _logger.LogInformation("💻 Comando: hostname");
                _logger.LogInformation("");

                // Crear credenciales seguras
                var securePassword = new System.Security.SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                var credential = new PSCredential(usuario, securePassword);

                // Configurar conexión remota
                var connectionInfo = new WSManConnectionInfo(
                    new Uri($"http://{ipEquipo}:5985/wsman"),
                    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                    credential
                );

                connectionInfo.OperationTimeout = 10000; // 10 segundos
                connectionInfo.OpenTimeout = 10000;

                // Conectar y ejecutar
                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    _logger.LogInformation("⏳ Estableciendo conexión...");
                    runspace.Open();
                    _logger.LogInformation("✅ Conexión establecida");

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;
                        ps.AddCommand("hostname");

                        _logger.LogInformation("⏳ Ejecutando comando...");
                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            var errores = new List<string>();
                            foreach (var error in ps.Streams.Error)
                            {
                                errores.Add(error.ToString());
                                _logger.LogError("❌ Error: {Error}", error);
                            }

                            resultado.Success = false;
                            resultado.Output = string.Join("\n", errores);
                            resultado.ErrorMessage = "Error al ejecutar comando";
                        }
                        else
                        {
                            var output = resultados.FirstOrDefault()?.ToString() ?? "";

                            resultado.Success = true;
                            resultado.Output = output;

                            _logger.LogInformation("✅ Comando ejecutado exitosamente");
                            _logger.LogInformation("📋 Resultado: {Output}", output);
                        }
                    }
                }

                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("");
            }
            catch (PSRemotingTransportException ex)
            {
                resultado.Success = false;
                resultado.ErrorMessage = "Error de autenticación o conexión";
                resultado.Output = ex.Message;
                _logger.LogError(ex, "❌ Error de conexión remota");

                resultado.Sugerencias.Add("• Verificar usuario y contraseña");
                resultado.Sugerencias.Add("• Verificar que WinRM está habilitado en el equipo remoto");
                resultado.Sugerencias.Add("• Ejecutar en el equipo remoto: Enable-PSRemoting -Force");
            }
            catch (Exception ex)
            {
                resultado.Success = false;
                resultado.ErrorMessage = $"Error inesperado: {ex.GetType().Name}";
                resultado.Output = ex.Message;
                _logger.LogError(ex, "❌ Error inesperado");
            }

            return resultado;
        }

        /// <summary>
        /// Listar archivos .exe en carpeta remota
        /// </summary>
        public async Task<RemoteCommandResult> TestListarArchivos(
            string ipEquipo,
            string usuario,
            string password,
            string rutaRemota)
        {
            var resultado = new RemoteCommandResult
            {
                IpEquipo = ipEquipo,
                Comando = $"Get-ChildItem '{rutaRemota}' -Filter *.exe"
            };

            try
            {
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("🔍 PRUEBA 2: Listar Archivos Remotos");
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("📍 Equipo: {IP}", ipEquipo);
                _logger.LogInformation("📁 Ruta: {Ruta}", rutaRemota);
                _logger.LogInformation("");

                var securePassword = new System.Security.SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                var credential = new PSCredential(usuario, securePassword);

                var connectionInfo = new WSManConnectionInfo(
                    new Uri($"http://{ipEquipo}:5985/wsman"),
                    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                    credential
                );

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    runspace.Open();

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        ps.AddCommand("Get-ChildItem")
                          .AddParameter("Path", rutaRemota)
                          .AddParameter("Filter", "*.exe");

                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            var errores = new List<string>();
                            foreach (var error in ps.Streams.Error)
                            {
                                errores.Add(error.ToString());
                            }

                            resultado.Success = false;
                            resultado.Output = string.Join("\n", errores);
                        }
                        else
                        {
                            var archivos = new List<string>();
                            foreach (dynamic archivo in resultados)
                            {
                                string nombre = archivo.Name.ToString();
                                archivos.Add(nombre);
                                _logger.LogInformation("📄 {Nombre}", nombre);
                            }

                            resultado.Success = true;
                            resultado.Output = $"Archivos encontrados: {archivos.Count}\n" +
                                              string.Join("\n", archivos);
                            resultado.Data = archivos;

                            _logger.LogInformation("✅ Total archivos: {Count}", archivos.Count);
                        }
                    }
                }

                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("");
            }
            catch (Exception ex)
            {
                resultado.Success = false;
                resultado.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Error al listar archivos");
            }

            return resultado;
        }

        /// <summary>
        /// Ejecutar script (obtener info del sistema)
        /// </summary>
        public async Task<RemoteCommandResult> TestScriptComplejo(
            string ipEquipo,
            string usuario,
            string password)
        {
            var resultado = new RemoteCommandResult
            {
                IpEquipo = ipEquipo,
                Comando = "Script de información del sistema"
            };

            try
            {
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("🔍 PRUEBA 3: Script Complejo");
                _logger.LogInformation("═══════════════════════════════════════════════");

                var securePassword = new System.Security.SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                var credential = new PSCredential(usuario, securePassword);

                var connectionInfo = new WSManConnectionInfo(
                    new Uri($"http://{ipEquipo}:5985/wsman"),
                    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                    credential
                );

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    runspace.Open();

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        // Script que obtiene información del sistema
                        string script = @"
                            $info = @{
                                'NombreEquipo' = $env:COMPUTERNAME
                                'SistemaOperativo' = (Get-CimInstance Win32_OperatingSystem).Caption
                                'Version' = (Get-CimInstance Win32_OperatingSystem).Version
                                'Arquitectura' = $env:PROCESSOR_ARCHITECTURE
                                'RAM_GB' = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
                                'Fabricante' = (Get-CimInstance Win32_ComputerSystem).Manufacturer
                            }
                            $info | ConvertTo-Json
                        ";

                        ps.AddScript(script);

                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            resultado.Success = false;
                            var errores = ps.Streams.Error.Select(e => e.ToString()).ToList();
                            resultado.Output = string.Join("\n", errores);
                        }
                        else
                        {
                            var output = resultados.FirstOrDefault()?.ToString() ?? "";

                            resultado.Success = true;
                            resultado.Output = output;

                            _logger.LogInformation("✅ Información del sistema:");
                            _logger.LogInformation("{Info}", output);
                        }
                    }
                }

                _logger.LogInformation("═══════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                resultado.Success = false;
                resultado.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Error al ejecutar script");
            }

            return resultado;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MODELOS
    // ═══════════════════════════════════════════════════════════

    public class RemoteCommandResult
    {
        public bool Success { get; set; }
        public string IpEquipo { get; set; } = string.Empty;
        public string Comando { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public List<string> Sugerencias { get; set; } = new();
        public object? Data { get; set; }
    }
}