using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using F2X.VersionReader.API.Models;

namespace F2X.VersionReader.API.Services
{
    /// <summary>
    /// Servicio para leer versiones y metadatos de archivos ejecutables (local y remoto)
    /// </summary>
    public class FileVersionService
    {
        private readonly ILogger<FileVersionService> _logger;

        public FileVersionService(ILogger<FileVersionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Formatea bytes que Windows Explorer
        /// </summary>
        private string FormatFileSizeWindows(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} bytes";
            }

            double kb = bytes / 1024d;
            double mb = bytes / 1048576d;
            double gb = bytes / 1073741824d;

            // KB
            if (kb < 1024)
            {
                if (kb < 10)
                {
                    long truncated = (long)(kb * 100);
                    double result = truncated / 100.0;
                    return result.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " KB";
                }
                else if (kb < 100)
                {
                    long truncated = (long)(kb * 10);
                    double result = truncated / 10.0;
                    return result.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " KB";
                }
                else
                {
                    return ((long)kb).ToString(System.Globalization.CultureInfo.InvariantCulture) + " KB";
                }
            }

            // MB
            if (mb < 1024)
            {
                if (mb < 10)
                {
                    double mbTimes100 = mb * 100;
                    long truncated = (long)Math.Floor(mbTimes100);

                    double remainder = mbTimes100 - truncated;
                    if (remainder > 0 && remainder < 0.02)
                    {
                        truncated = truncated - 1;
                    }

                    double result = truncated / 100.0;
                    return result.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " MB";
                }
                else
                {
                    long mbTruncated = (long)Math.Floor(mb);
                    double decimalPart = mb - mbTruncated;

                    if (decimalPart < 0.05)
                    {
                        return mbTruncated.ToString(System.Globalization.CultureInfo.InvariantCulture) + " MB";
                    }
                    else
                    {
                        long truncated = (long)(mb * 10);
                        double result = truncated / 10.0;
                        return result.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
                    }
                }
            }

            // GB
            long truncatedGb = (long)(gb * 100);
            double resultGb = truncatedGb / 100.0;
            return resultGb.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " GB";
        }

        /// <summary>
        /// Normaliza el nombre de una carpeta eliminando timestamps y sufijos
        /// </summary>
        private string NormalizarNombreCarpeta(string nombreCarpeta)
        {
            var patron = @"-\d{8}T\d{6}Z-\d+-\d+$";
            var nombreLimpio = System.Text.RegularExpressions.Regex.Replace(nombreCarpeta, patron, "");
            return nombreLimpio.ToLowerInvariant().Trim();
        }

        /// <summary>
        /// Obtiene las carpetas que existen en ambas versiones (Actual y Futura)
        /// </summary>
        public List<(string carpetaActual, string carpetaFutura)> ObtenerCarpetasCoincidentes(
            string rutaVersionActual,
            string rutaVersionFutura)
        {
            var coincidencias = new List<(string, string)>();

            var carpetasActual = Directory.GetDirectories(rutaVersionActual)
                .Select(path => new
                {
                    RutaCompleta = path,
                    Nombre = Path.GetFileName(path),
                    NombreNormalizado = NormalizarNombreCarpeta(Path.GetFileName(path) ?? "")
                })
                .ToList();

            var carpetasFutura = Directory.GetDirectories(rutaVersionFutura)
                .Select(path => new
                {
                    RutaCompleta = path,
                    Nombre = Path.GetFileName(path),
                    NombreNormalizado = NormalizarNombreCarpeta(Path.GetFileName(path) ?? "")
                })
                .ToList();

            foreach (var carpetaActual in carpetasActual)
            {
                var carpetaFutura = carpetasFutura
                    .FirstOrDefault(f => f.NombreNormalizado == carpetaActual.NombreNormalizado);

                if (carpetaFutura != null)
                {
                    _logger.LogInformation("✅ Coincidencia encontrada:");
                    _logger.LogInformation("   Actual: {Actual}", carpetaActual.Nombre);
                    _logger.LogInformation("   Futura: {Futura}", carpetaFutura.Nombre);
                    coincidencias.Add((carpetaActual.RutaCompleta, carpetaFutura.RutaCompleta));
                }
                else
                {
                    _logger.LogWarning("⚠️ Carpeta solo en Actual: {Nombre}", carpetaActual.Nombre);
                }
            }

            foreach (var carpetaFutura in carpetasFutura)
            {
                var existe = carpetasActual
                    .Any(a => a.NombreNormalizado == carpetaFutura.NombreNormalizado);

                if (!existe)
                    _logger.LogWarning("⚠️ Carpeta solo en Futura: {Nombre}", carpetaFutura.Nombre);
            }

            return coincidencias;
        }

        /// <summary>
        /// Escanea un directorio local y retorna información de todos los archivos .exe
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

                List<string> rutasAEscanear;

                if (request.CarpetasEspecificas != null && request.CarpetasEspecificas.Any())
                {
                    _logger.LogInformation("📁 Escaneando {Count} carpetas específicas", request.CarpetasEspecificas.Count);
                    rutasAEscanear = request.CarpetasEspecificas;
                }
                else
                {
                    rutasAEscanear = new List<string> { request.Directory };
                }

                var allFiles = new List<string>();

                foreach (var ruta in rutasAEscanear)
                {
                    if (Directory.Exists(ruta))
                    {
                        var searchOption = request.IncludeSubdirectories
                            ? SearchOption.AllDirectories
                            : SearchOption.TopDirectoryOnly;

                        var files = Directory.GetFiles(ruta, request.SearchPattern, searchOption);
                        allFiles.AddRange(files);

                        _logger.LogInformation("   📂 {Carpeta}: {Count} archivos",
                            Path.GetFileName(ruta), files.Length);
                    }
                }

                _logger.LogInformation("Encontrados {Count} archivos totales", allFiles.Count);

                var tasks = allFiles.Select(filePath =>
                    Task.Run(() => GetFileInfo(filePath, request.Directory)
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

                _logger.LogInformation("Escaneo completado en {Ms}ms. Total: {Count} archivos",
                    response.ScanTimeMs, response.TotalFiles);

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
                _logger.LogInformation("🌐 ESCANEO REMOTO - Equipo: {IP} - Ruta: {Ruta}",
                    ipEquipo, rutaRemota);

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
                    runspace.Open();

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
                                $sizeBytes = $file.Length
                                $sizeDisplay = """"

                                if ($sizeBytes -lt 1024) {{
                            $sizeDisplay = ""$sizeBytes bytes""
                        }}
                        elseif ($sizeBytes -lt 1048576) {{
                            # KB
                            $sizeKB = $sizeBytes / 1024.0

                            if ($sizeKB -lt 10) {{
                                $truncated = [Math]::Floor($sizeKB * 100) / 100
                                $sizeDisplay = ""$($truncated.ToString('0.00')) KB""
                            }}
                            elseif ($sizeKB -lt 100) {{
                                $truncated = [Math]::Floor($sizeKB * 10) / 10
                                $sizeDisplay = ""$($truncated.ToString('0.0')) KB""
                            }}
                            else {{
                                $sizeDisplay = ""$([Math]::Floor($sizeKB)) KB""
                            }}
                        }}
                        elseif ($sizeBytes -lt 1073741824) {{
                            # MB
                            $sizeMB = $sizeBytes / 1048576.0
    
                            if ($sizeMB -lt 10) {{
                                # Algoritmo especial para MB < 10
                                $mbTimes100 = $sizeMB * 100
                                $truncated = [Math]::Floor($mbTimes100)
                                $remainder = $mbTimes100 - $truncated
        
                                if ($remainder -gt 0 -and $remainder -lt 0.02) {{
                                    $truncated = $truncated - 1
                                }}
        
                                $result = $truncated / 100.0
                                $sizeDisplay = ""$($result.ToString('0.00')) MB""
                            }}
                            else {{
                                # Algoritmo para MB >= 10
                                $mbTruncated = [Math]::Floor($sizeMB)
                                $decimalPart = $sizeMB - $mbTruncated
        
                                if ($decimalPart -lt 0.05) {{
                                    $sizeDisplay = ""$mbTruncated MB""
                                }}
                                else {{
                                    $truncated = [Math]::Floor($sizeMB * 10) / 10
                                    $sizeDisplay = ""$($truncated.ToString('0.0')) MB""
                                }}
                            }}
                        }}
                        else {{
                            # GB
                            $sizeGB = $sizeBytes / 1073741824.0
                            $truncated = [Math]::Floor($sizeGB * 100) / 100
                            $sizeDisplay = ""$($truncated.ToString('0.00')) GB""
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
                            }}
                            catch {{
                                Write-Warning ""Error procesando archivo: $($file.FullName)""
                            }}
                        }}

                        $results | ConvertTo-Json -Depth 3
                        ";

                        ps.AddScript(script);

                        var resultados = await Task.Run(() => ps.Invoke());

                        if (ps.HadErrors)
                        {
                            var errores = ps.Streams.Error.Select(e => e.ToString()).ToList();
                            response.Success = false;
                            response.Error = string.Join("\n", errores);
                            response.Message = "Error al ejecutar escaneo remoto";
                        }
                        else
                        {
                            var jsonOutput = resultados.FirstOrDefault()?.ToString() ?? "[]";
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
                    return files;

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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error parseando archivo individual");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error parseando JSON");
            }

            return files;
        }

        private string GetJsonString(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
                return prop.ValueKind == System.Text.Json.JsonValueKind.String
                    ? prop.GetString() ?? string.Empty
                    : string.Empty;

            return string.Empty;
        }

        private int GetJsonInt(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
                return prop.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? prop.GetInt32()
                    : 0;

            return 0;
        }

        private long GetJsonLong(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
                return prop.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? prop.GetInt64()
                    : 0L;

            return 0L;
        }

        private ExeFileInfo? GetFileInfo(string filePath, string baseDirectory)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // DIAGNÓSTICO TEMPORAL
                if (fileInfo.Name.Contains("Squirrel-Mono"))
                {
                    _logger.LogInformation("🔍 DEBUG Squirrel-Mono:");
                    _logger.LogInformation("   Length: {Length} bytes", fileInfo.Length);
                    _logger.LogInformation("   Expected: 1856000 bytes");
                }
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                var sizeDisplay = FormatFileSizeWindows(fileInfo.Length);

                var relativePath = Path.GetRelativePath(baseDirectory, filePath);

                var version = $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}";

                _logger.LogInformation("📄 {FileName} - v{Version} - {Size}",
                    fileInfo.Name, version, sizeDisplay);

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