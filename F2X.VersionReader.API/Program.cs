using F2X.VersionReader.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel para usar puerto 7001
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7001, listenOptions =>  
    {
        listenOptions.UseHttps();
    });
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
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Mensaje de inicio
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 F2X Version Reader API iniciada");
logger.LogInformation("🔍 Health check: https://localhost:7000/api/versionscanner/health");
logger.LogInformation("🔍 Endpoint de escaneo: https://localhost:7000/api/versionscanner/scan");
logger.LogInformation("🔍 Endpoint multi-equipo: https://localhost:7000/api/multiequiposcan/scan");
logger.LogInformation("✅ Backend listo para recibir peticiones del frontend");

app.Run();