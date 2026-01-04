using Automais.Core.Interfaces;
using Automais.Core.Services;
using Automais.Infrastructure.ChirpStack;
using Automais.Infrastructure.Data;
using Automais.Infrastructure.Repositories;
using Automais.Infrastructure.RouterOS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ===== Configuração de Serviços =====

// Substituir variáveis de ambiente no formato ${VAR} nas configurações
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

Console.WriteLine($"🔍 Connection string original: {MaskConnectionString(connectionString)}");

// Verificar quais variáveis foram encontradas ANTES da substituição
var envVars = new[] { "DB_HOST", "DB_PORT", "DB_NAME", "DB_USER", "DB_PASSWORD" };
Console.WriteLine("🔍 Verificando variáveis de ambiente:");
var missingVars = new List<string>();
foreach (var varName in envVars)
{
    var value = Environment.GetEnvironmentVariable(varName);
    if (string.IsNullOrEmpty(value))
    {
        Console.WriteLine($"  ❌ {varName}: NÃO DEFINIDA");
        missingVars.Add(varName);
    }
    else
    {
        Console.WriteLine($"  ✅ {varName}: {(varName.Contains("PASSWORD") ? "***" : value)}");
    }
}

// Se todas as variáveis estão definidas, fazer a substituição
// Caso contrário, tentar construir a connection string diretamente
string baseConnectionString;
if (missingVars.Any())
{
    Console.WriteLine($"⚠️ Variáveis faltando: {string.Join(", ", missingVars)}");
    Console.WriteLine("🔧 Tentando construir connection string diretamente das variáveis...");
    
    // Tentar construir a connection string diretamente
    var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "";
    var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "";
    var username = Environment.GetEnvironmentVariable("DB_USER") ?? "";
    var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
    
    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || 
        string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            $"Não foi possível construir a connection string. Variáveis faltando: {string.Join(", ", missingVars)}. " +
            $"Verifique se as variáveis estão configuradas no systemd service.");
    }
    
    baseConnectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};Ssl Mode=Require";
    Console.WriteLine($"✅ Connection string construída diretamente: {MaskConnectionString(baseConnectionString)}");
}
else
{
    // Substituir variáveis de ambiente no formato ${VAR}
    baseConnectionString = ReplaceEnvironmentVariables(connectionString);
    Console.WriteLine($"✅ Connection string após substituição: {MaskConnectionString(baseConnectionString)}");
}

// Validar se a connection string tem host
if (string.IsNullOrWhiteSpace(baseConnectionString))
{
    throw new InvalidOperationException("Connection string está vazia após substituição de variáveis de ambiente.");
}

// Verificar se a connection string tem Host
if (!baseConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) && 
    !baseConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Connection string não contém Host ou Server. Verifique a configuração.");
}

var rootCertSetting = builder.Configuration["Database:RootCertificatePath"];

// Validar e construir connection string
NpgsqlConnectionStringBuilder npgBuilder;
try
{
    npgBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
    {
        SslMode = SslMode.Require,
        TrustServerCertificate = true,
        CommandTimeout = 60, // Timeout para comandos SQL (60 segundos)
        Timeout = 30, // Timeout para estabelecer conexão (30 segundos)
        ConnectionIdleLifetime = 300, // Fechar conexões idle após 5 minutos
        ConnectionPruningInterval = 10, // Verificar conexões idle a cada 10 segundos
        MaxPoolSize = 100, // Máximo de conexões no pool
        MinPoolSize = 5 // Mínimo de conexões no pool
    };

    // Validar se o Host foi configurado
    if (string.IsNullOrWhiteSpace(npgBuilder.Host))
    {
        throw new InvalidOperationException(
            "Connection string não contém Host. " +
            "Verifique se a variável de ambiente está configurada corretamente. " +
            $"Connection string (parcial): {MaskConnectionString(baseConnectionString)}");
    }
}
catch (ArgumentException ex)
{
    throw new InvalidOperationException(
        $"Erro ao processar connection string: {ex.Message}. " +
        $"Verifique se a connection string está no formato correto. " +
        $"Connection string (parcial): {MaskConnectionString(baseConnectionString)}", ex);
}

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
    {
        opt.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        opt.CommandTimeout(60); // Timeout adicional para comandos EF Core
    });
    // REMOVIDO: UseSnakeCaseNamingConvention() 
    // O banco usa PascalCase (Id, Name, TenantId), não snake_case
    // options.UseSnakeCaseNamingConvention();
});

// ChirpStack Client (gRPC)
var chirpStackConfig = builder.Configuration.GetSection("ChirpStack");
var chirpStackUrl = ReplaceEnvironmentVariables(chirpStackConfig["ApiUrl"] ?? "http://srv01.automais.io:8080");
var chirpStackToken = ReplaceEnvironmentVariables(chirpStackConfig["ApiToken"] ?? "");

// Validar URL do ChirpStack
if (string.IsNullOrWhiteSpace(chirpStackUrl))
{
    Console.WriteLine("⚠️ ChirpStack URL não configurada. Algumas funcionalidades podem não funcionar.");
}
else
{
    // Validar formato da URL
    if (!Uri.TryCreate(chirpStackUrl, UriKind.Absolute, out var uri))
    {
        Console.WriteLine($"⚠️ ChirpStack URL inválida: {chirpStackUrl}");
    }
    else
    {
        Console.WriteLine($"🔗 ChirpStack URL (gRPC): {chirpStackUrl}");
    }
}

Console.WriteLine($"🔑 Token configurado: {(!string.IsNullOrEmpty(chirpStackToken) ? "Sim ✅" : "Não ⚠️")}");

builder.Services.AddSingleton<IChirpStackClient>(sp => 
{
    var logger = sp.GetService<ILogger<ChirpStackClient>>();
    try
    {
        return new ChirpStackClient(chirpStackUrl, chirpStackToken, logger);
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "Erro ao criar ChirpStackClient");
        throw;
    }
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
builder.Services.AddScoped<IRouterAllowedNetworkRepository, RouterAllowedNetworkRepository>();
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
builder.Services.AddScoped<IWireGuardServerService, Automais.Infrastructure.WireGuard.WireGuardServerService>();

// RouterBackupService com caminho de storage configurável
var backupStoragePath = builder.Configuration["Backup:StoragePath"] ?? "/backups/routers";
builder.Services.AddScoped<IRouterBackupService>(sp =>
{
    var backupRepo = sp.GetRequiredService<IRouterBackupRepository>();
    var routerRepo = sp.GetRequiredService<IRouterRepository>();
    var routerOsClient = sp.GetRequiredService<IRouterOsClient>();
    var tenantUserRepo = sp.GetService<ITenantUserRepository>();
    return new RouterBackupService(backupRepo, routerRepo, routerOsClient, tenantUserRepo, backupStoragePath);
});

// External Clients
builder.Services.AddSingleton<IRouterOsClient>(sp =>
{
    var logger = sp.GetService<ILogger<RouterOsClient>>();
    return new RouterOsClient(logger);
});

// Controllers com serialização JSON configurada
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serializar enums como strings ao invés de números
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // Ignorar propriedades nulas
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

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

// CORS (para desenvolvimento e produção)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000", 
                "http://localhost:5173",
                "https://automais.io",
                "https://www.automais.io"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ===== Configuração do Pipeline HTTP =====

// Tratamento global de erros (deve vir primeiro)
app.UseExceptionHandler("/error");

// Swagger sempre habilitado
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Automais IoT Platform API v1");
    c.RoutePrefix = "swagger"; // Swagger em /swagger
});

// CORS deve vir antes de UseAuthorization
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Endpoint de tratamento de erros
app.Map("/error", (HttpContext context) =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var response = new
    {
        message = exception?.Message ?? "Erro interno do servidor",
        detail = app.Environment.IsDevelopment() ? exception?.ToString() : null
    };
    return Results.Json(response, statusCode: 500);
});

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
/// Mascara informações sensíveis da connection string para logs
/// </summary>
static string MaskConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return "(vazia)";

    // Mascara senha e outros dados sensíveis
    var masked = connectionString;
    var patterns = new[] { "Password=", "Pwd=", "User ID=", "Username=", "User=" };
    
    foreach (var pattern in patterns)
    {
        var index = masked.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var start = index + pattern.Length;
            var end = masked.IndexOf(';', start);
            if (end < 0) end = masked.Length;
            
            var length = end - start;
            masked = masked.Substring(0, start) + new string('*', Math.Min(length, 10)) + masked.Substring(end);
        }
    }
    
    return masked;
}

/// <summary>
/// Substitui variáveis de ambiente no formato ${VAR} pelos valores reais
/// </summary>
static string ReplaceEnvironmentVariables(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return input;

    var result = input;
    var startIndex = 0;
    var missingVars = new List<string>();

    while ((startIndex = result.IndexOf("${", startIndex)) != -1)
    {
        var endIndex = result.IndexOf("}", startIndex);
        if (endIndex == -1)
        {
            Console.WriteLine($"⚠️ Variável de ambiente malformada: {result.Substring(startIndex)}");
            break;
        }

        var varName = result.Substring(startIndex + 2, endIndex - startIndex - 2);
        var envValue = Environment.GetEnvironmentVariable(varName);
        
        if (string.IsNullOrEmpty(envValue))
        {
            Console.WriteLine($"❌ Variável de ambiente '{varName}' não encontrada!");
            missingVars.Add(varName);
            envValue = string.Empty; // Substitui por string vazia para não quebrar o formato
        }
        else
        {
            Console.WriteLine($"✅ Variável '{varName}' encontrada (valor mascarado)");
        }
        
        result = result.Substring(0, startIndex) + envValue + result.Substring(endIndex + 1);
        startIndex += envValue.Length;
    }

    if (missingVars.Any())
    {
        throw new InvalidOperationException(
            $"Variáveis de ambiente não encontradas: {string.Join(", ", missingVars)}. " +
            "Verifique se as variáveis estão configuradas no systemd service ou no ambiente.");
    }

    return result;
}

