using System.Diagnostics;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using F2X.VersionReader.API.Models;
using System.Runtime.InteropServices;
using System.Text;


namespace F2X.VersionReader.API.Services
{

    public static class WindowsSizeFormatter
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern long StrFormatByteSizeW(
            long fileSize,
            StringBuilder buffer,
            int bufferSize
        );

        public static string Format(long bytes)
        {
            StringBuilder sb = new StringBuilder(32);
            StrFormatByteSizeW(bytes, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Servicio para leer versiones y metadatos de archivos ejecutables (local y remoto)
    /// </summary>
    public class FileVersionService
    {
        private readonly ILogger<FileVersionService> _logger;

        // Cultura española para formato de decimales con COMA
        private static readonly CultureInfo SpanishCulture = new CultureInfo("es-ES");

        public FileVersionService(ILogger<FileVersionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Formatea el tamaño del archivo EXACTAMENTE como Windows lo muestra
        /// </summary>
        private string FormatFileSizeWindows(long bytes)
        {
            if (bytes < 1024) // < 1 KB
            {
                return $"{bytes} Bytes";
            }
            else if (bytes < 1048576) // < 1 MB
            {
                double kb = (double)bytes / 1024.0;

                if (kb < 10)
                {
                    // < 10 KB: 2 decimales con coma (truncado)
                    long kbInt = (long)Math.Truncate(kb);
                    long kbDec = (long)Math.Truncate((kb - kbInt) * 100.0);
                    return $"{kbInt},{kbDec:D2} KB";
                }
                else if (kb < 100)
                {
                    // 10-99 KB: 1 decimal con coma (truncado)
                    long kbInt = (long)Math.Truncate(kb);
                    long kbDec = (long)Math.Truncate((kb - kbInt) * 10.0);
                    return $"{kbInt},{kbDec} KB";
                }
                else
                {
                    // >= 100 KB: sin decimales
                    long kbTruncated = (long)Math.Truncate(kb);
                    return $"{kbTruncated} KB";
                }
            }
            else if (bytes < 1073741824) // < 1 GB
            {
                double mb = (double)bytes / 1048576.0;

                if (mb >= 100)
                {
                    // >= 100 MB: sin decimales
                    long mbTruncated = (long)Math.Truncate(mb);
                    return $"{mbTruncated} MB";
                }
                else if (mb < 10)
                {
                    // Construir el string manualmente
                    long mbInt = (long)Math.Truncate(mb);
                    long mbDec = (long)Math.Truncate((mb - mbInt) * 100.0);

                    _logger.LogDebug("FormatSize: {Bytes} bytes → {MB} MB → Int: {Int} Dec: {Dec}",
                        bytes, mb, mbInt, mbDec);

                    return $"{mbInt},{mbDec:D2} MB";
                }
                else
                {
                    // 10-99 MB: 1 decimal con coma (truncado)
                    long mbInt = (long)Math.Truncate(mb);
                    long mbDec = (long)Math.Truncate((mb - mbInt) * 10.0);

                    if (mbDec == 0)
                    {
                        return $"{mbInt} MB";
                    }
                    else
                    {
                        return $"{mbInt},{mbDec} MB";
                    }
                }
            }
            else // >= 1 GB
            {
                double gb = (double)bytes / 1073741824.0;

                if (gb >= 100)
                {
                    long gbTruncated = (long)Math.Truncate(gb);
                    return $"{gbTruncated} GB";
                }
                else if (gb < 10)
                {
                    long gbInt = (long)Math.Truncate(gb);
                    long gbDec = (long)Math.Truncate((gb - gbInt) * 100.0);
                    return $"{gbInt},{gbDec:D2} GB";
                }
                else
                {
                    long gbInt = (long)Math.Truncate(gb);
                    long gbDec = (long)Math.Truncate((gb - gbInt) * 10.0);

                    if (gbDec == 0)
                    {
                        return $"{gbInt} GB";
                    }
                    else
                    {
                        return $"{gbInt},{gbDec} GB";
                    }
                }
            }
        }

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

                        // Script PowerShell con truncamiento exacto
                        string script = $@"
                        $files = Get-ChildItem -Path '{rutaRemota}' -Filter '{searchPattern}' -Recurse:{recurseValue} -File -ErrorAction SilentlyContinue

                        $results = @()
                        foreach ($file in $files) {{
                            try {{
                                $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
                                
                                $sizeBytes = $file.Length
                                $sizeDisplay = """"
                                
                                if ($sizeBytes -lt 1024) {{
                                    $sizeDisplay = ""$sizeBytes Bytes""
                                }} elseif ($sizeBytes -lt 1048576) {{
                                    # KB
                                    $sizeKB = $sizeBytes / 1024.0
                                    
                                    if ($sizeKB -lt 10) {{
                                        $kbInt = [Math]::Truncate($sizeKB)
                                        $kbDec = [Math]::Truncate(($sizeKB - $kbInt) * 100.0)
                                        $sizeDisplay = ""$kbInt,$($kbDec.ToString('00')) KB""
                                    }} elseif ($sizeKB -lt 100) {{
                                        $kbInt = [Math]::Truncate($sizeKB)
                                        $kbDec = [Math]::Truncate(($sizeKB - $kbInt) * 10.0)
                                        $sizeDisplay = ""$kbInt,$kbDec KB""
                                    }} else {{
                                        $kbTruncated = [Math]::Truncate($sizeKB)
                                        $sizeDisplay = ""$kbTruncated KB""
                                    }}
                                }} elseif ($sizeBytes -lt 1073741824) {{
                                    # MB
                                    $sizeMB = $sizeBytes / 1048576.0
                                    
                                    if ($sizeMB -ge 100) {{
                                        $mbTruncated = [Math]::Truncate($sizeMB)
                                        $sizeDisplay = ""$mbTruncated MB""
                                    }} elseif ($sizeMB -lt 10) {{
                                        $mbInt = [Math]::Truncate($sizeMB)
                                        $mbDec = [Math]::Truncate(($sizeMB - $mbInt) * 100.0)
                                        $sizeDisplay = ""$mbInt,$($mbDec.ToString('00')) MB""
                                    }} else {{
                                        $mbInt = [Math]::Truncate($sizeMB)
                                        $mbDec = [Math]::Truncate(($sizeMB - $mbInt) * 10.0)
                                        
                                        if ($mbDec -eq 0) {{
                                            $sizeDisplay = ""$mbInt MB""
                                        }} else {{
                                            $sizeDisplay = ""$mbInt,$mbDec MB""
                                        }}
                                    }}
                                }} else {{
                                    # GB
                                    $sizeGB = $sizeBytes / 1073741824.0
                                    
                                    if ($sizeGB -ge 100) {{
                                        $gbTruncated = [Math]::Truncate($sizeGB)
                                        $sizeDisplay = ""$gbTruncated GB""
                                    }} elseif ($sizeGB -lt 10) {{
                                        $gbInt = [Math]::Truncate($sizeGB)
                                        $gbDec = [Math]::Truncate(($sizeGB - $gbInt) * 100.0)
                                        $sizeDisplay = ""$gbInt,$($gbDec.ToString('00')) GB""
                                    }} else {{
                                        $gbInt = [Math]::Truncate($sizeGB)
                                        $gbDec = [Math]::Truncate(($sizeGB - $gbInt) * 10.0)
                                        
                                        if ($gbDec -eq 0) {{
                                            $sizeDisplay = ""$gbInt GB""
                                        }} else {{
                                            $sizeDisplay = ""$gbInt,$gbDec GB""
                                        }}
                                    }}
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

                var sizeDisplay = WindowsSizeFormatter.Format(fileInfo.Length);


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