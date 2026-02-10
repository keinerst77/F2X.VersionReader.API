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

                connectionInfo.OperationTimeout = 10000;
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


        /// <summary>
        /// Validar si un directorio existe en el equipo remoto
        /// </summary>
        public async Task<RemoteCommandResult> ValidarDirectorioRemoto(
            string ipEquipo,
            string usuario,
            string password,
            string rutaDirectorio)
        {
            var resultado = new RemoteCommandResult
            {
                IpEquipo = ipEquipo,
                Comando = $"Test-Path '{rutaDirectorio}'"
            };

            try
            {
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("🔍 VALIDACIÓN DE DIRECTORIO REMOTO");
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("📍 Equipo: {IP}", ipEquipo);
                _logger.LogInformation("📁 Ruta: {Ruta}", rutaDirectorio);
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

                connectionInfo.OperationTimeout = 10000;
                connectionInfo.OpenTimeout = 10000;

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    _logger.LogInformation("⏳ Estableciendo conexión...");
                    runspace.Open();
                    _logger.LogInformation("✅ Conexión establecida");

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        // Script para validar si el directorio existe
                        string script = $@"
                    $exists = Test-Path -Path '{rutaDirectorio}' -PathType Container
                    
                    if ($exists) {{
                        $itemCount = (Get-ChildItem -Path '{rutaDirectorio}' -Filter *.exe -Recurse -ErrorAction SilentlyContinue | Measure-Object).Count
                        
                        return @{{
                            Exists = $true
                            Path = '{rutaDirectorio}'
                            ExeCount = $itemCount
                        }} | ConvertTo-Json
                    }} else {{
                        return @{{
                            Exists = $false
                            Path = '{rutaDirectorio}'
                            ExeCount = 0
                        }} | ConvertTo-Json
                    }}
                ";

                        ps.AddScript(script);

                        _logger.LogInformation("⏳ Validando directorio...");
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
                            resultado.ErrorMessage = "Error al validar directorio";
                        }
                        else
                        {
                            var output = resultados.FirstOrDefault()?.ToString() ?? "";

                            resultado.Success = true;
                            resultado.Output = output;

                            // Intentar parsear el JSON para determinar si existe
                            try
                            {
                                var jsonDoc = System.Text.Json.JsonDocument.Parse(output);
                                var exists = jsonDoc.RootElement.GetProperty("Exists").GetBoolean();
                                var exeCount = jsonDoc.RootElement.GetProperty("ExeCount").GetInt32();

                                if (exists)
                                {
                                    _logger.LogInformation("✅ Directorio existe");
                                    _logger.LogInformation("📋 Archivos .exe encontrados: {Count}", exeCount);
                                    resultado.Sugerencias.Add($"✅ El directorio existe y contiene {exeCount} archivo(s) .exe");
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ Directorio no existe");
                                    resultado.Sugerencias.Add("⚠️ El directorio no existe en el equipo remoto");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "No se pudo parsear la respuesta JSON");
                            }
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
                resultado.Sugerencias.Add("• Verificar que WinRM está habilitado");
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
        /// Buscar directorio por nombre en ubicaciones comunes
        /// </summary>
        public async Task<RemoteCommandResult> BuscarDirectorioPorNombre(
            string ipEquipo,
            string usuario,
            string password,
            string nombreCarpeta)
        {
            var resultado = new RemoteCommandResult
            {
                IpEquipo = ipEquipo,
                Comando = $"Buscar carpeta: {nombreCarpeta}"
            };

            try
            {
                _logger.LogInformation("🔍 Buscando carpeta '{Nombre}' en equipo remoto: {IP}", nombreCarpeta, ipEquipo);

                var securePassword = new System.Security.SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                var credential = new PSCredential(usuario, securePassword);

                var connectionInfo = new WSManConnectionInfo(
                    new Uri($"http://{ipEquipo}:5985/wsman"),
                    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
                    credential
                );

                connectionInfo.OperationTimeout = 30000; // 30 segundos para búsqueda
                connectionInfo.OpenTimeout = 10000;

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    runspace.Open();

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        // Script para buscar en ubicaciones comunes
                        string script = $@"
                    $folderName = '{nombreCarpeta}'
                    $searchPaths = @(
                        ""$env:USERPROFILE\Desktop"",
                        ""$env:USERPROFILE\Documents"",
                        ""$env:USERPROFILE\Downloads"",
                        ""C:\"",
                        ""D:\""
                    )

                    $found = $null
                    foreach ($basePath in $searchPaths) {{
                        if (Test-Path $basePath) {{
                            $fullPath = Join-Path $basePath $folderName
                            if (Test-Path -Path $fullPath -PathType Container) {{
                                $itemCount = (Get-ChildItem -Path $fullPath -Filter *.exe -Recurse -ErrorAction SilentlyContinue | Measure-Object).Count
                                $found = @{{
                                    Exists = $true
                                    Path = $fullPath
                                    ExeCount = $itemCount
                                    SearchedIn = $basePath
                                }}
                                break
                            }}
                        }}
                    }}

                    if ($found) {{
                        return $found | ConvertTo-Json
                    }} else {{
                        return @{{
                            Exists = $false
                            Path = $folderName
                            ExeCount = 0
                            SearchedPaths = $searchPaths -join '; '
                        }} | ConvertTo-Json
                    }}
                ";

                        ps.AddScript(script);

                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            resultado.Success = false;
                            var errores = ps.Streams.Error.Select(e => e.ToString()).ToList();
                            resultado.Output = string.Join("\n", errores);
                            resultado.ErrorMessage = "Error al buscar directorio";
                        }
                        else
                        {
                            var output = resultados.FirstOrDefault()?.ToString() ?? "";
                            resultado.Success = true;
                            resultado.Output = output;

                            _logger.LogInformation("✅ Búsqueda completada: {Output}", output);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Success = false;
                resultado.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Error al buscar directorio remoto por nombre");
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