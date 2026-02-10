namespace F2X.VersionReader.API.Models
{
    /// <summary>
    /// Representa la información de un archivo ejecutable
    /// </summary>
    public class ExeFileInfo
    {
        /// <summary>
        /// Nombre del archivo
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Nombre normalizado en minúsculas para comparación
        /// </summary>
        public string NameNormalized { get; set; } = string.Empty;

        /// <summary>
        /// Ruta relativa desde el directorio base
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Ruta completa del archivo
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Versión del archivo
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Tamaño del archivo en bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Tamaño del archivo formateado
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de última modificación
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Fecha de última modificación formateada
        /// </summary>
        public string LastModifiedString { get; set; } = string.Empty;

        /// <summary>
        /// Descripción del archivo (metadato de Windows)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Compañía/Empresa (metadato de Windows)
        /// </summary>
        public string? Company { get; set; }

        /// <summary>
        /// Nombre del producto (metadato de Windows)
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// IP del equipo de origen (usado en escaneos remotos)
        /// </summary>
        public string? SourceIp { get; set; }

        /// <summary>
        /// Lista de IPs de equipos donde existe este archivo
        /// </summary>
        public List<string>? EquiposIps { get; set; }
    }
}