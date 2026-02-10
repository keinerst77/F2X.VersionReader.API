# F2X - Generador de Ficha Técnica

## Instalación y Configuración del Backend

Sigue estos pasos en orden para instalar y ejecutar el backend:

### Paso 1: Clonar el Repositorio del Backend

Abre Git Bash y ejecuta:
```bash
git clone https://github.com/keinerst77/F2X.VersionReader.API.git
```

### Paso 2: Abrir el Proyecto en Visual Studio

1. Navega a la carpeta donde clonaste el repositorio
2. Busca el archivo con extensión `.sln`
3. Haz doble clic en el archivo `.sln` para abrirlo con Visual Studio

### Paso 3: Configurar Puertos

1. En el explorador de soluciones busca la carpeta `Properties`
2. Encontrarás un archivo llamado `launchSettings.json`
3. Cambia el contenido del archivo por esto:
```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:19546",
      "sslPort": 44394
    }
  },
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:7000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7000;http://localhost:7000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

4. Guarda los cambios

### Paso 4: Confiar en el Certificado de Desarrollo

1. Abre PowerShell como Administrador
2. Ejecuta el siguiente comando:
```powershell
dotnet dev-certs https --trust
```

3. Confirma cuando se te solicite instalar el certificado

### Paso 5: Ejecutar el Backend

Con el proyecto abierto en Visual Studio:

1. Presiona `F5` (o `Fn + F5` en algunos teclados)
2. Espera a que aparezca una consola que indique:
```
Now listening on: http://localhost:5000
Now listening on: https://localhost:7000
```

**Importante:** Mantén esta ventana abierta mientras uses la aplicación.

### Paso 6: Abrir el Ejecutable

1. Navega a la carpeta donde descargaste el archivo `.exe`
2. Haz doble clic en el `.exe` para abrir la aplicación

---

## ¡Listo!

La aplicación ya debería estar funcionando correctamente.

**Versión:** 1.2.0  
**Última actualización:** Enero 2026
