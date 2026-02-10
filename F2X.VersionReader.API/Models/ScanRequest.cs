using System.ComponentModel.DataAnnotations;

namespace F2X.VersionReader.API.Models
{
    /// <summary>
    /// Petición para escanear un directorio
    /// </summary>
    public class ScanRequest
    {
        /// <summary>
        /// Ruta del directorio a escanear
        /// </summary>
        [Required(ErrorMessage = "La ruta del directorio es requerida")]
        public string Directory { get; set; } = string.Empty;

        /// <summary>
        /// Si debe incluir subdirectorios (búsqueda recursiva)
        /// </summary>
        public bool IncludeSubdirectories { get; set; } = true;

        /// <summary>
        /// Patrón de búsqueda (por defecto "*.exe")
        /// </summary>
        public string SearchPattern { get; set; } = "*.exe";

        /// <summary>
        /// Lista de carpetas específicas a escanear (rutas completas)
        /// Si está vacía, escanea todo el directorio
        /// </summary>
        public List<string>? CarpetasEspecificas { get; set; }
    }
}