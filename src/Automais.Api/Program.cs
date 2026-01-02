using Automais.Core.Interfaces;
using Automais.Core.Services;
using Automais.Infrastructure.ChirpStack;
using Automais.Infrastructure.Data;
using Automais.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ===== Configuração de Serviços =====

// Substituir variáveis de ambiente no formato ${VAR} nas configurações
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

// Substituir variáveis de ambiente no formato ${VAR}
var baseConnectionString = ReplaceEnvironmentVariables(connectionString);

var rootCertSetting = builder.Configuration["Database:RootCertificatePath"];
var npgBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
{
    SslMode = SslMode.Require,
    TrustServerCertificate = true
};

if (!string.IsNullOrWhiteSpace(rootCertSetting))
{
    var rootCertPath = Path.IsPathRooted(rootCertSetting)
        ? rootCertSetting
        : Path.Combine(builder.Environment.ContentRootPath, rootCertSetting);

    if (File.Exists(rootCertPath))
    {
        Console.WriteLine($"🔐 Certificado raiz encontrado em {rootCertPath}. Validando SSL.");
        npgBuilder.RootCertificate = rootCertPath;
        npgBuilder.TrustServerCertificate = false;
        npgBuilder.SslMode = SslMode.VerifyFull;
    }
    else
    {
        Console.WriteLine($"⚠️ Certificado raiz não encontrado em {rootCertPath}. Usando TrustServerCertificate=true.");
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(npgBuilder.ConnectionString, opt =>
        opt.EnableRetryOnFailure());
    options.UseSnakeCaseNamingConvention();
});

// ChirpStack Client (gRPC)
var chirpStackConfig = builder.Configuration.GetSection("ChirpStack");
var chirpStackUrl = ReplaceEnvironmentVariables(chirpStackConfig["ApiUrl"] ?? "http://srv01.automais.io:8080");
var chirpStackToken = ReplaceEnvironmentVariables(chirpStackConfig["ApiToken"] ?? "");

Console.WriteLine($"🔗 ChirpStack URL (gRPC): {chirpStackUrl}");
Console.WriteLine($"🔑 Token configurado: {(!string.IsNullOrEmpty(chirpStackToken) ? "Sim ✅" : "Não ⚠️")}");

builder.Services.AddSingleton<IChirpStackClient>(sp => 
{
    var logger = sp.GetService<ILogger<ChirpStackClient>>();
    return new ChirpStackClient(chirpStackUrl, chirpStackToken, logger);
});

// Repositórios (EF Core)
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IGatewayRepository, GatewayRepository>();
builder.Services.AddScoped<ITenantUserRepository, TenantUserRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IVpnNetworkRepository, VpnNetworkRepository>();
builder.Services.AddScoped<IRouterRepository, RouterRepository>();
builder.Services.AddScoped<IRouterWireGuardPeerRepository, RouterWireGuardPeerRepository>();
builder.Services.AddScoped<IRouterConfigLogRepository, RouterConfigLogRepository>();
builder.Services.AddScoped<IRouterBackupRepository, RouterBackupRepository>();

// Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IGatewayService, GatewayService>();
builder.Services.AddScoped<ITenantUserService, TenantUserService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IVpnNetworkService, VpnNetworkService>();
builder.Services.AddScoped<IRouterService, RouterService>();
builder.Services.AddScoped<IRouterWireGuardService, RouterWireGuardService>();
builder.Services.AddScoped<IRouterBackupService, RouterBackupService>();

// External Clients
builder.Services.AddSingleton<IRouterOsClient, RouterOsClient>();

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Automais IoT Platform API", 
        Version = "v1",
        Description = "API para gerenciamento de plataforma IoT multi-tenant (PostgreSQL)"
    });
});

// CORS (para desenvolvimento)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== Configuração do Pipeline HTTP =====

// Swagger sempre habilitado
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Automais IoT Platform API v1");
    c.RoutePrefix = "swagger"; // Swagger em /swagger
});

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new 
{
    status = "healthy",
    mode = "database",
    database = "postgresql (DigitalOcean)",
    chirpstack = chirpStackUrl,
    timestamp = DateTime.UtcNow
}));

Console.WriteLine("\n🚀 API rodando!");
Console.WriteLine($"📝 Swagger: http://localhost:5000/swagger ou https://localhost:5001/swagger");
Console.WriteLine($"❤️  Health: http://localhost:5000/health");
Console.WriteLine($"💾 Modo: Postgres (DigitalOcean)");
Console.WriteLine($"📡 ChirpStack: {chirpStackUrl}\n");

app.Run();

// ===== Helper Functions =====

/// <summary>
/// Substitui variáveis de ambiente no formato ${VAR} pelos valores reais
/// </summary>
static string ReplaceEnvironmentVariables(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return input;

    var result = input;
    var startIndex = 0;

    while ((startIndex = result.IndexOf("${", startIndex)) != -1)
    {
        var endIndex = result.IndexOf("}", startIndex);
        if (endIndex == -1)
            break;

        var varName = result.Substring(startIndex + 2, endIndex - startIndex - 2);
        var envValue = Environment.GetEnvironmentVariable(varName) ?? string.Empty;
        
        result = result.Substring(0, startIndex) + envValue + result.Substring(endIndex + 1);
        startIndex += envValue.Length;
    }

    return result;
}

