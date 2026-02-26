using F2X.VersionReader.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel para usar puerto 7001 
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7001);
});

// Agregar servicios al contenedor
builder.Services.AddControllers();

// Registrar servicios personalizados
builder.Services.AddScoped<FileVersionService>();
builder.Services.AddScoped<PowerShellRemoteValidationService>();
builder.Services.AddScoped<MultiEquipoScanService>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configurar logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Middleware
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Mensaje de inicio
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 F2X Version Reader API iniciada");
logger.LogInformation("🔍 Health check:      http://localhost:7001/api/versionscanner/health");
logger.LogInformation("🔍 Escaneo local:     http://localhost:7001/api/versionscanner/scan");
logger.LogInformation("🔍 Multi-equipo:      http://localhost:7001/api/multiequiposcan/scan");
logger.LogInformation("✅ Backend listo para recibir peticiones del frontend");

app.Run();