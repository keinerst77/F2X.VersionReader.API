using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using F2X.VersionReader.API.Models;

namespace F2X.VersionReader.API.Services
{
    /// <summary>
    /// Servicio para leer versiones y metadatos de archivos ejecutables (local y remoto)
    /// VERSIÓN FINAL CORREGIDA: Replica EXACTAMENTE el comportamiento de Windows Propiedades > Detalles
    /// </summary>
    public class FileVersionService
    {
        private readonly ILogger<FileVersionService> _logger;

        public FileVersionService(ILogger<FileVersionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Formatea el tamaño del archivo EXACTAMENTE como Windows lo muestra en Propiedades > Detalles
        /// 
        /// ALGORITMO EXACTO DE WINDOWS (pestaña Detalles):
        /// 1. Para archivos < 100 KB: Muestra con decimales (0.0#)
        /// 2. Para archivos >= 100 KB: TRUNCA (Math.Floor) - NO redondea
        /// 
        /// Ejemplos CORREGIDOS:
        /// - 362,496 bytes = 354.0 KB → muestra "354 KB"
        /// - 377,856 bytes = 368.90625 KB → trunca a "368 KB" (NO 369, NO 370)
        /// - 14,336 bytes = 14.0 KB → muestra "14,0 KB"
        /// - 143,360 bytes = 140.0 KB → muestra "140 KB"
        /// </summary>
        private string FormatFileSizeWindows(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} Bytes";
            }
            else if (bytes < 1048576) // Menos de 1 MB
            {
                // Calcular KB con decimales
                double kb = (double)bytes / 1024.0;

                if (kb < 100)
                {
                    // Archivos pequeños (< 100 KB): mostrar con decimales
                    return $"{kb:0.0#} KB";
                }
                else
                {
                    // Archivos >= 100 KB: Windows usa TRUNCAMIENTO (Math.Floor)
                    long kbTruncated = (long)Math.Floor(kb);
                    return $"{kbTruncated} KB";
                }
            }
            else if (bytes < 1073741824) // Menos de 1 GB
            {
                double mb = (double)bytes / 1048576.0;
                return $"{mb:0.##} MB";
            }
            else
            {
                double gb = (double)bytes / 1073741824.0;
                return $"{gb:0.##} GB";
            }
        }

        /// <summary>
        /// Escanea un directorio LOCAL y retorna información de todos los archivos .exe
        /// </summary>
        public async Task<ScanResponse> ScanDirectoryAsync(ScanRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new ScanResponse
            {
                Directory = request.Directory
            };

            try
            {
                if (!Directory.Exists(request.Directory))
                {
                    response.Success = false;
                    response.Error = $"El directorio '{request.Directory}' no existe";
                    response.Message = "Directorio no encontrado";
                    return response;
                }

                _logger.LogInformation("Escaneando directorio LOCAL: {Directory}", request.Directory);

                var searchOption = request.IncludeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var exeFiles = Directory.GetFiles(
                    request.Directory,
                    request.SearchPattern,
                    searchOption
                );

                _logger.LogInformation("Encontrados {Count} archivos", exeFiles.Length);

                var tasks = exeFiles.Select(filePath => Task.Run(() =>
                    GetFileInfo(filePath, request.Directory)
                ));

                var fileInfos = await Task.WhenAll(tasks);

                response.Files = fileInfos
                    .Where(f => f != null)
                    .OrderBy(f => f!.Name)
                    .ToList()!;

                stopwatch.Stop();

                response.Success = true;
                response.TotalFiles = response.Files.Count;
                response.ScanTimeMs = stopwatch.ElapsedMilliseconds;
                response.Message = $"Escaneo completado exitosamente. {response.TotalFiles} archivo(s) encontrado(s)";

                _logger.LogInformation(
                    "Escaneo completado en {Ms}ms. Total: {Count} archivos",
                    response.ScanTimeMs,
                    response.TotalFiles
                );

                return response;
            }
            catch (UnauthorizedAccessException ex)
            {
                response.Success = false;
                response.Error = "Acceso denegado al directorio";
                response.Message = "No tienes permisos para acceder a este directorio";
                _logger.LogError(ex, "Error de permisos al escanear directorio");
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.Message;
                response.Message = "Error al escanear el directorio";
                _logger.LogError(ex, "Error inesperado al escanear directorio");
                return response;
            }
        }

        /// <summary>
        /// Escanea un directorio REMOTO usando PowerShell Remoting
        /// </summary>
        public async Task<ScanResponse> ScanDirectoryRemoteAsync(
            string ipEquipo,
            string usuario,
            string password,
            string rutaRemota,
            bool includeSubdirectories = true,
            string searchPattern = "*.exe")
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new ScanResponse
            {
                Directory = $"\\\\{ipEquipo}\\{rutaRemota}"
            };

            try
            {
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("🌐 ESCANEO REMOTO");
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("📍 Equipo: {IP}", ipEquipo);
                _logger.LogInformation("📁 Ruta: {Ruta}", rutaRemota);
                _logger.LogInformation("🔍 Patrón: {Pattern}", searchPattern);
                _logger.LogInformation("📂 Subdirectorios: {Include}", includeSubdirectories);
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

                connectionInfo.OperationTimeout = 60000;
                connectionInfo.OpenTimeout = 10000;

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    _logger.LogInformation("⏳ Estableciendo conexión...");
                    runspace.Open();
                    _logger.LogInformation("✅ Conexión establecida");

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        string recurseValue = includeSubdirectories ? "$true" : "$false";

                        string script = $@"
                        $files = Get-ChildItem -Path '{rutaRemota}' -Filter '{searchPattern}' -Recurse:{recurseValue} -File -ErrorAction SilentlyContinue

                        $results = @()
                        foreach ($file in $files) {{
                            try {{
                                $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
                                
                                # Calcular tamaño EXACTAMENTE como Windows lo muestra
                                $sizeBytes = $file.Length
                                $sizeDisplay = """"
                                
                                if ($sizeBytes -lt 1024) {{
                                    $sizeDisplay = ""$sizeBytes Bytes""
                                }} elseif ($sizeBytes -lt 1048576) {{
                                    # Calcular KB
                                    $sizeKB = $sizeBytes / 1024.0
                                    
                                    if ($sizeKB -lt 100) {{
                                        # Archivos < 100 KB: mostrar con decimales
                                        $sizeDisplay = ""$($sizeKB.ToString('0.0#')) KB""
                                    }} else {{
                                        # Archivos >= 100 KB: usar TRUNCAMIENTO (Floor)
                                        $sizeKBTruncated = [Math]::Floor($sizeKB)
                                        $sizeDisplay = ""$sizeKBTruncated KB""
                                    }}
                                }} elseif ($sizeBytes -lt 1073741824) {{
                                    $sizeMB = $sizeBytes / 1048576.0
                                    $sizeDisplay = ""$($sizeMB.ToString('0.##')) MB""
                                }} else {{
                                    $sizeGB = $sizeBytes / 1073741824.0
                                    $sizeDisplay = ""$($sizeGB.ToString('0.##')) GB""
                                }}
                                
                                $results += [PSCustomObject]@{{
                                    Name = $file.Name
                                    FullPath = $file.FullName
                                    RelativePath = $file.FullName.Replace('{rutaRemota}', '').TrimStart('\')
                                    SizeBytes = $file.Length
                                    SizeDisplay = $sizeDisplay
                                    LastModified = $file.LastWriteTime.ToString('yyyy-MM-ddTHH:mm:ss')
                                    FileMajorPart = $versionInfo.FileMajorPart
                                    FileMinorPart = $versionInfo.FileMinorPart
                                    FileBuildPart = $versionInfo.FileBuildPart
                                    FilePrivatePart = $versionInfo.FilePrivatePart
                                    FileDescription = $versionInfo.FileDescription
                                    CompanyName = $versionInfo.CompanyName
                                    ProductName = $versionInfo.ProductName
                                }}
                            }} catch {{
                                Write-Warning ""Error procesando archivo: $($file.FullName)""
                            }}
                        }}

                        $results | ConvertTo-Json -Depth 3
                        ";

                        ps.AddScript(script);

                        _logger.LogInformation("⏳ Ejecutando escaneo remoto...");
                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            var errores = new List<string>();
                            foreach (var error in ps.Streams.Error)
                            {
                                errores.Add(error.ToString());
                                _logger.LogError("❌ Error: {Error}", error);
                            }

                            response.Success = false;
                            response.Error = string.Join("\n", errores);
                            response.Message = "Error al ejecutar escaneo remoto";
                        }
                        else
                        {
                            var jsonOutput = resultados.FirstOrDefault()?.ToString() ?? "[]";

                            _logger.LogInformation("📋 JSON recibido ({Length} caracteres)", jsonOutput.Length);

                            var archivos = ParseRemoteFiles(jsonOutput);

                            response.Files = archivos;
                            response.Success = true;
                            response.TotalFiles = archivos.Count;
                            response.Message = $"Escaneo remoto completado. {archivos.Count} archivo(s) encontrado(s)";

                            _logger.LogInformation("✅ Archivos procesados: {Count}", archivos.Count);
                        }
                    }
                }

                stopwatch.Stop();
                response.ScanTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("⏱️ Tiempo total: {Ms}ms", response.ScanTimeMs);
                _logger.LogInformation("═══════════════════════════════════════════════");
                _logger.LogInformation("");

                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.Message;
                response.Message = "Error al escanear directorio remoto";
                _logger.LogError(ex, "❌ Error inesperado al escanear directorio remoto");

                stopwatch.Stop();
                response.ScanTimeMs = stopwatch.ElapsedMilliseconds;

                return response;
            }
        }

        private List<ExeFileInfo> ParseRemoteFiles(string jsonOutput)
        {
            var files = new List<ExeFileInfo>();

            try
            {
                if (jsonOutput.Trim() == "[]")
                {
                    return files;
                }

                var json = System.Text.Json.JsonDocument.Parse(jsonOutput);

                var elementos = json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? json.RootElement.EnumerateArray()
                    : new[] { json.RootElement }.AsEnumerable();

                foreach (var elemento in elementos)
                {
                    try
                    {
                        var name = GetJsonString(elemento, "Name");
                        var fullPath = GetJsonString(elemento, "FullPath");
                        var relativePath = GetJsonString(elemento, "RelativePath");
                        var sizeBytes = GetJsonLong(elemento, "SizeBytes");
                        var sizeDisplay = GetJsonString(elemento, "SizeDisplay");
                        var lastModifiedStr = GetJsonString(elemento, "LastModified");

                        var major = GetJsonInt(elemento, "FileMajorPart");
                        var minor = GetJsonInt(elemento, "FileMinorPart");
                        var build = GetJsonInt(elemento, "FileBuildPart");
                        var revision = GetJsonInt(elemento, "FilePrivatePart");

                        var description = GetJsonString(elemento, "FileDescription");
                        var company = GetJsonString(elemento, "CompanyName");
                        var productName = GetJsonString(elemento, "ProductName");

                        var version = $"{major}.{minor}.{build}.{revision}";

                        DateTime lastModified = DateTime.TryParse(lastModifiedStr, out var parsedDate)
                            ? parsedDate
                            : DateTime.MinValue;

                        files.Add(new ExeFileInfo
                        {
                            Name = name,
                            NameNormalized = name.ToLowerInvariant(),
                            RelativePath = relativePath,
                            FullPath = fullPath,
                            Version = version,
                            SizeBytes = sizeBytes,
                            Size = sizeDisplay,
                            LastModified = lastModified,
                            LastModifiedString = lastModified.ToString("dd/MM/yyyy HH:mm:ss"),
                            Description = description,
                            Company = company,
                            ProductName = productName
                        });

                        _logger.LogInformation("  ✅ {Name} - v{Version} - {Size}", name, version, sizeDisplay);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error parseando archivo individual");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error parseando JSON: {Json}", jsonOutput);
            }

            return files;
        }

        private string GetJsonString(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == System.Text.Json.JsonValueKind.String
                    ? prop.GetString() ?? string.Empty
                    : string.Empty;
            }
            return string.Empty;
        }

        private int GetJsonInt(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? prop.GetInt32()
                    : 0;
            }
            return 0;
        }

        private long GetJsonLong(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? prop.GetInt64()
                    : 0L;
            }
            return 0L;
        }

        private ExeFileInfo? GetFileInfo(string filePath, string baseDirectory)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                var sizeDisplay = FormatFileSizeWindows(fileInfo.Length);

                _logger.LogInformation("📄 Archivo: {FileName} - Versión: {Version} - Tamaño: {Size}",
                    fileInfo.Name,
                    $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}",
                    sizeDisplay);

                var relativePath = Path.GetRelativePath(baseDirectory, filePath);

                var version = $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}";

                return new ExeFileInfo
                {
                    Name = fileInfo.Name,
                    NameNormalized = fileInfo.Name.ToLowerInvariant(),
                    RelativePath = relativePath,
                    FullPath = filePath,
                    Version = version,
                    SizeBytes = fileInfo.Length,
                    Size = sizeDisplay,
                    LastModified = fileInfo.LastWriteTime,
                    LastModifiedString = fileInfo.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss"),
                    Description = versionInfo.FileDescription,
                    Company = versionInfo.CompanyName,
                    ProductName = versionInfo.ProductName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERROR al leer archivo: {File}", filePath);
                return null;
            }
        }
    }
}