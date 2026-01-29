using F2X.VersionReader.API.Services;
var builder = WebApplication.CreateBuilder(args);
// Agregar servicios al contenedor
builder.Services.AddControllers();
// Registrar servicios personalizados
builder.Services.AddScoped<FileVersionService>();
// Configurar CORS para permitir peticiones desde el frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5500",  // Live Server
            "http://127.0.0.1:5500",  // Live Server alternativo
            "http://localhost:3000",  // Otro puerto común
            "file://"                 // Archivos locales
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .SetIsOriginAllowed(origin => true) // Permitir cualquier origen en desarrollo
        .AllowCredentials();
    });
});
// Configurar logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddScoped<PowerShellRemoteValidationService>();
var app = builder.Build();
// IMPORTANTE: CORS debe ir antes de Authorization
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
// Mensaje de inicio
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 F2X Version Reader API iniciada");
logger.LogInformation("🔍 Health check: https://localhost:7000/api/versionscanner/health");
logger.LogInformation("🔍 Endpoint de escaneo: https://localhost:7000/api/versionscanner/scan");
logger.LogInformation("✅ Backend listo para recibir peticiones del frontend");
app.Run();