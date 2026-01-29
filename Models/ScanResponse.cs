namespace F2X.VersionReader.API.Models
{
    /// <summary>
    /// Respuesta del escaneo de directorios
    /// </summary>
    public class ScanResponse
    {
        /// <summary>
        /// Si la operación fue exitosa
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Mensaje descriptivo del resultado
        /// </summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// Directorio escaneado
        /// </summary>
        public string Directory { get; set; } = string.Empty;
        /// <summary>
        /// Total de archivos .exe encontrados
        /// </summary>
        public int TotalFiles { get; set; }
        /// <summary>
        /// Lista de archivos encontrados
        /// </summary>
        public List<ExeFileInfo> Files { get; set; } = new();
        /// <summary>
        /// Tiempo que tomó el escaneo en milisegundos
        /// </summary>
        public long ScanTimeMs { get; set; }
        /// <summary>
        /// Mensaje de error (si hay)
        /// </summary>
        public string? Error { get; set; }
    }
}