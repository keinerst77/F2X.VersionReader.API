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
                // Validar que el directorio existe
                if (!Directory.Exists(request.Directory))
                {
                    response.Success = false;
                    response.Error = $"El directorio '{request.Directory}' no existe";
                    response.Message = "Directorio no encontrado";
                    return response;
                }

                _logger.LogInformation("Escaneando directorio LOCAL: {Directory}", request.Directory);

                // Buscar archivos .exe
                var searchOption = request.IncludeSubdirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var exeFiles = Directory.GetFiles(
                    request.Directory,
                    request.SearchPattern,
                    searchOption
                );

                _logger.LogInformation("Encontrados {Count} archivos", exeFiles.Length);

                // Procesar cada archivo en paralelo para mejor rendimiento
                var tasks = exeFiles.Select(filePath => Task.Run(() =>
                    GetFileInfo(filePath, request.Directory)
                ));

                var fileInfos = await Task.WhenAll(tasks);

                // Filtrar nulos (archivos que no se pudieron leer)
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

                connectionInfo.OperationTimeout = 60000; // 60 segundos para escaneos grandes
                connectionInfo.OpenTimeout = 10000;

                using (Runspace runspace = RunspaceFactory.CreateRunspace(connectionInfo))
                {
                    _logger.LogInformation("⏳ Estableciendo conexión...");
                    runspace.Open();
                    _logger.LogInformation("✅ Conexión establecida");

                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        // Script que obtiene archivos .exe con toda la información necesaria
                        string recurseValue = includeSubdirectories ? "$true" : "$false";

                        string script = $@"
                        $files = Get-ChildItem -Path '{rutaRemota}' -Filter '{searchPattern}' -Recurse:{recurseValue} -File -ErrorAction SilentlyContinue

                            $results = @()
                            foreach ($file in $files) {{
                                try {{
                                    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
                                    
                                    $results += [PSCustomObject]@{{
                                        Name = $file.Name
                                        FullPath = $file.FullName
                                        RelativePath = $file.FullName.Replace('{rutaRemota}', '').TrimStart('\')
                                        SizeBytes = $file.Length
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

                            // Parsear JSON a objetos
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

        /// <summary>
        /// Parsea el JSON recibido del escaneo remoto
        /// </summary>
        private List<ExeFileInfo> ParseRemoteFiles(string jsonOutput)
        {
            var files = new List<ExeFileInfo>();

            try
            {
                // Si el resultado es un array vacío
                if (jsonOutput.Trim() == "[]")
                {
                    return files;
                }

                var json = System.Text.Json.JsonDocument.Parse(jsonOutput);

                // Manejar tanto arrays como objetos únicos
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
                        var lastModifiedStr = GetJsonString(elemento, "LastModified");

                        var major = GetJsonInt(elemento, "FileMajorPart");
                        var minor = GetJsonInt(elemento, "FileMinorPart");
                        var build = GetJsonInt(elemento, "FileBuildPart");
                        var revision = GetJsonInt(elemento, "FilePrivatePart");

                        var description = GetJsonString(elemento, "FileDescription");
                        var company = GetJsonString(elemento, "CompanyName");
                        var productName = GetJsonString(elemento, "ProductName");

                        // Construir versión
                        var version = $"{major}.{minor}.{build}.{revision}";

                        // Parsear fecha
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
                            Size = FormatFileSize(sizeBytes),
                            LastModified = lastModified,
                            LastModifiedString = lastModified.ToString("dd/MM/yyyy HH:mm:ss"),
                            Description = description,
                            Company = company,
                            ProductName = productName
                        });

                        _logger.LogInformation("  ✅ {Name} - v{Version}", name, version);
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

        /// <summary>
        /// Obtiene un string de un JsonElement de forma segura
        /// </summary>
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

        /// <summary>
        /// Obtiene un int de un JsonElement de forma segura
        /// </summary>
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

        /// <summary>
        /// Obtiene un long de un JsonElement de forma segura
        /// </summary>
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

        /// <summary>
        /// Obtiene información detallada de un archivo ejecutable LOCAL
        /// </summary>
        private ExeFileInfo? GetFileInfo(string filePath, string baseDirectory)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                _logger.LogInformation("📄 Archivo: {FileName} - Versión: {Version}",
                    fileInfo.Name,
                    $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.{versionInfo.FilePrivatePart}");

                // Calcular ruta relativa
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
                    Size = FormatFileSize(fileInfo.Length),
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

        /// <summary>
        /// Formatea el tamaño del archivo a formato legible (KB, MB, GB)
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "Bytes", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}