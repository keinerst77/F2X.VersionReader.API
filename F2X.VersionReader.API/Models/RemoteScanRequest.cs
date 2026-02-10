using System.ComponentModel.DataAnnotations;

namespace F2X.VersionReader.API.Models
{
    /// <summary>
    /// Petición para escanear un directorio remoto usando PowerShell Remoting
    /// </summary>
    public class RemoteScanRequest
    {
        /// <summary>
        /// Dirección IP del equipo remoto
        /// </summary>
        [Required(ErrorMessage = "La IP del equipo es requerida")]
        public string IpEquipo { get; set; } = string.Empty;

        /// <summary>
        /// Usuario con permisos de administrador en el equipo remoto
        /// </summary>
        [Required(ErrorMessage = "El usuario es requerido")]
        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña del usuario
        /// </summary>
        [Required(ErrorMessage = "La contraseña es requerida")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Ruta del directorio a escanear en el equipo remoto
        /// </summary>
        [Required(ErrorMessage = "La ruta remota es requerida")]
        public string RutaRemota { get; set; } = string.Empty;

        /// <summary>
        /// Si debe incluir subdirectorios (búsqueda recursiva)
        /// </summary>
        public bool IncludeSubdirectories { get; set; } = true;

        /// <summary>
        /// Patrón de búsqueda (por defecto "*.exe")
        /// </summary>
        public string SearchPattern { get; set; } = "*.exe";
    }
}