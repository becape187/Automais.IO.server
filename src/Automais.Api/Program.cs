using Automais.Core.Configuration;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Core.Services;
using Automais.Infrastructure.ChirpStack;
using Automais.Infrastructure.Data;
using Automais.Infrastructure.Repositories;
using Automais.Infrastructure.RouterOS;
using Automais.Infrastructure.Services;
using Automais.Infrastructure.WireGuard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net;
using System.Security.Cryptography.X509Certificates;

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
        CommandTimeout = 30, // Timeout para comandos SQL (30 segundos - reduzido de 60)
        Timeout = 15, // Timeout para estabelecer conexão (15 segundos - reduzido de 30)
        ConnectionIdleLifetime = 180, // Fechar conexões idle após 3 minutos (reduzido de 5)
        ConnectionPruningInterval = 5, // Verificar conexões idle a cada 5 segundos (reduzido de 10)
        MaxPoolSize = 50, // Máximo de conexões no pool (reduzido de 100 para evitar esgotamento)
        MinPoolSize = 2 // Mínimo de conexões no pool (reduzido de 5)
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

string? finalCertPath = null;

// Tentar primeiro o caminho configurado
if (!string.IsNullOrWhiteSpace(rootCertSetting))
{
    var rootCertPath = Path.IsPathRooted(rootCertSetting)
        ? rootCertSetting
        : Path.Combine(builder.Environment.ContentRootPath, rootCertSetting);

    if (File.Exists(rootCertPath))
    {
        finalCertPath = rootCertPath;
    }
}

// Se não encontrou, tentar no diretório pai (fixo no servidor)
if (string.IsNullOrEmpty(finalCertPath))
{
    var parentDirCertPath = Path.Combine(
        Path.GetDirectoryName(builder.Environment.ContentRootPath) ?? string.Empty,
        "ca-certificate.crt");
    
    if (File.Exists(parentDirCertPath))
    {
        finalCertPath = parentDirCertPath;
        Console.WriteLine($"🔍 Certificado encontrado no diretório pai: {finalCertPath}");
    }
}

// Se ainda não encontrou, tentar caminho absoluto fixo (Linux)
if (string.IsNullOrEmpty(finalCertPath))
{
    var fixedPath = "/root/automais.io/ca-certificate.crt";
    if (File.Exists(fixedPath))
    {
        finalCertPath = fixedPath;
        Console.WriteLine($"🔍 Certificado encontrado no caminho fixo: {finalCertPath}");
    }
}

// Aplicar certificado se encontrado
if (!string.IsNullOrEmpty(finalCertPath))
{
    Console.WriteLine($"🔐 Certificado raiz encontrado em {finalCertPath}. Validando SSL.");
    npgBuilder.RootCertificate = finalCertPath;
    npgBuilder.TrustServerCertificate = false;
    npgBuilder.SslMode = SslMode.VerifyFull;
}
else
{
    Console.WriteLine($"⚠️ Certificado raiz não encontrado em nenhum local. Usando TrustServerCertificate=true.");
    Console.WriteLine($"⚠️ Locais verificados:");
    if (!string.IsNullOrWhiteSpace(rootCertSetting))
    {
        var rootCertPath = Path.IsPathRooted(rootCertSetting)
            ? rootCertSetting
            : Path.Combine(builder.Environment.ContentRootPath, rootCertSetting);
        Console.WriteLine($"   - {rootCertPath}");
    }
    var parentDirCertPath = Path.Combine(
        Path.GetDirectoryName(builder.Environment.ContentRootPath) ?? string.Empty,
        "ca-certificate.crt");
    Console.WriteLine($"   - {parentDirCertPath}");
    Console.WriteLine($"   - /root/automais.io/ca-certificate.crt");
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
builder.Services.AddScoped<IUserAllowedRouteRepository, Automais.Infrastructure.Repositories.UserAllowedRouteRepository>();
builder.Services.AddScoped<IRouterConfigLogRepository, RouterConfigLogRepository>();
builder.Services.AddScoped<IRouterBackupRepository, RouterBackupRepository>();

// Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IGatewayService, GatewayService>();
builder.Services.AddScoped<ITenantUserService, TenantUserService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
// Configuração do WireGuard
builder.Services.Configure<WireGuardSettings>(
    builder.Configuration.GetSection("WireGuard"));

builder.Services.AddScoped<IVpnNetworkService, VpnNetworkService>();
builder.Services.AddScoped<IRouterService, RouterService>();
builder.Services.AddScoped<IWireGuardServerService, WireGuardServerService>();
builder.Services.AddScoped<IAuthService, Automais.Infrastructure.Services.AuthService>();
builder.Services.AddScoped<IUserVpnService, Automais.Infrastructure.Services.UserVpnService>();
builder.Services.AddScoped<IRouterWireGuardService>(sp =>
{
    var peerRepo = sp.GetRequiredService<IRouterWireGuardPeerRepository>();
    var routerRepo = sp.GetRequiredService<IRouterRepository>();
    var vpnNetworkRepo = sp.GetRequiredService<IVpnNetworkRepository>();
    var wireGuardServerService = sp.GetRequiredService<IWireGuardServerService>();
    var wireGuardSettings = sp.GetRequiredService<IOptions<WireGuardSettings>>();
    var logger = sp.GetService<ILogger<Automais.Core.Services.RouterWireGuardService>>();
    return new Automais.Core.Services.RouterWireGuardService(peerRepo, routerRepo, vpnNetworkRepo, wireGuardSettings, wireGuardServerService, logger);
});

// SignalR para notificações em tempo real
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Habilitar erros detalhados para debug
})
.AddJsonProtocol(jsonOptions =>
{
    // Usar camelCase para compatibilidade com JavaScript/TypeScript
    jsonOptions.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    jsonOptions.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Serviço de sincronização do WireGuard (executa na inicialização)
builder.Services.AddHostedService<WireGuardSyncService>();

// Serviço de monitoramento de status dos roteadores (executa periodicamente)
builder.Services.AddHostedService<RouterStatusMonitorService>();

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
        // Usar camelCase para compatibilidade com JavaScript/TypeScript
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Serializar enums como strings ao invés de números
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // Ignorar propriedades nulas
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configurar Kestrel com timeouts (evita requisições travadas)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    
    // Configurar HTTPS apenas em produção usando certificado Let's Encrypt
    if (builder.Environment.IsProduction())
    {
        var certPath = "/etc/letsencrypt/live/automais.io";
        var certFile = Path.Combine(certPath, "fullchain.pem");
        var keyFile = Path.Combine(certPath, "privkey.pem");
        
        if (File.Exists(certFile) && File.Exists(keyFile))
        {
            try
            {
                // Ler certificado e chave privada em formato PEM
                var certContent = File.ReadAllText(certFile);
                var keyContent = File.ReadAllText(keyFile);
                
                // Converter PEM para X509Certificate2
                var certificate = X509Certificate2.CreateFromPem(certContent, keyContent);
                
                // Configurar HTTPS na porta 5001
                options.Listen(IPAddress.Any, 5001, listenOptions =>
                {
                    listenOptions.UseHttps(certificate);
                });
                
                Console.WriteLine("✅ HTTPS configurado na porta 5001 usando certificado Let's Encrypt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao configurar HTTPS: {ex.Message}");
                Console.WriteLine("⚠️ Continuando apenas com HTTP (porta 5000)");
            }
        }
        else
        {
            Console.WriteLine($"⚠️ Certificados não encontrados em {certPath}");
            Console.WriteLine("⚠️ Continuando apenas com HTTP (porta 5000)");
        }
    }
    else
    {
        Console.WriteLine("🔧 Ambiente de desenvolvimento - HTTPS não configurado");
    }
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
                "https://www.automais.io",
                "https://automais.io:5001",
                "https://www.automais.io:5001"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Testar conexão com banco de dados na inicialização
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🔍 Iniciando teste de conexão com banco de dados...");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        logger.LogInformation("📊 Tentando conectar ao banco de dados...");
        logger.LogInformation("📊 Host: {Host}", npgBuilder.Host);
        logger.LogInformation("📊 Port: {Port}", npgBuilder.Port);
        logger.LogInformation("📊 Database: {Database}", npgBuilder.Database);
        logger.LogInformation("📊 Username: {Username}", npgBuilder.Username);
        logger.LogInformation("📊 SSL Mode: {SslMode}", npgBuilder.SslMode);
        logger.LogInformation("📊 Command Timeout: {CommandTimeout}s", npgBuilder.CommandTimeout);
        logger.LogInformation("📊 Connection Timeout: {Timeout}s", npgBuilder.Timeout);
        
        // Tentar conectar e capturar erros detalhados
        try
        {
            logger.LogInformation("🔄 Tentando CanConnectAsync()...");
            var canConnect = await dbContext.Database.CanConnectAsync();
            logger.LogInformation("🔄 CanConnectAsync() retornou: {Result}", canConnect);
            
            if (canConnect)
            {
                logger.LogInformation("✅ Conexão com banco de dados estabelecida com sucesso!");
                
                // Testar uma query simples
                try
                {
                    logger.LogInformation("🔄 Executando query de teste (COUNT tenants)...");
                    var tenantCount = await dbContext.Set<Tenant>().CountAsync();
                    logger.LogInformation("✅ Query de teste executada com sucesso! Total de tenants: {Count}", tenantCount);
                }
                catch (Exception queryEx)
                {
                    logger.LogWarning(queryEx, "⚠️ Conexão OK, mas query de teste falhou: {Error}", queryEx.Message);
                    logger.LogWarning("⚠️ Stack Trace: {StackTrace}", queryEx.StackTrace);
                }
            }
            else
            {
                logger.LogError("❌ CanConnectAsync retornou false - não foi possível conectar ao banco de dados!");
                
                // Tentar uma conexão direta para ver o erro real
                logger.LogInformation("🔄 Tentando conexão direta com ExecuteSqlRawAsync('SELECT 1')...");
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                    logger.LogInformation("✅ ExecuteSqlRawAsync funcionou mesmo com CanConnectAsync=false");
                }
                catch (Exception directEx)
                {
                    logger.LogError(directEx, "❌ Erro ao executar query direta: {Error}", directEx.Message);
                    logger.LogError("❌ Tipo de exceção: {ExceptionType}", directEx.GetType().Name);
                    if (directEx.InnerException != null)
                    {
                        logger.LogError("❌ Inner Exception: {InnerException}", directEx.InnerException.Message);
                        logger.LogError("❌ Inner Exception Type: {InnerExceptionType}", directEx.InnerException.GetType().Name);
                        logger.LogError("❌ Inner Stack Trace: {InnerStackTrace}", directEx.InnerException.StackTrace);
                    }
                    logger.LogError("❌ Stack Trace completo: {StackTrace}", directEx.StackTrace);
                }
            }
        }
        catch (Npgsql.NpgsqlException npgEx)
        {
            logger.LogError(npgEx, "❌ Erro Npgsql ao testar conexão: {Error}", npgEx.Message);
            logger.LogError("❌ SQL State: {SqlState}", npgEx.SqlState);
            logger.LogError("❌ Code: {Code}", npgEx.ErrorCode);
            logger.LogError("❌ Inner Exception: {InnerException}", npgEx.InnerException?.Message);
            logger.LogError("❌ Stack Trace: {StackTrace}", npgEx.StackTrace);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Erro inesperado ao testar conexão: {Error}", ex.Message);
            logger.LogError("❌ Tipo de exceção: {ExceptionType}", ex.GetType().Name);
            logger.LogError("❌ Inner Exception: {InnerException}", ex.InnerException?.Message);
            logger.LogError("❌ Stack Trace: {StackTrace}", ex.StackTrace);
        }
    }
}
catch (Npgsql.NpgsqlException ex)
{
    logger.LogError(ex, "❌ Erro Npgsql ao conectar ao banco de dados: {Error}", ex.Message);
    logger.LogError("❌ Inner Exception: {InnerException}", ex.InnerException?.Message);
    logger.LogError("❌ SQL State: {SqlState}", ex.SqlState);
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Erro inesperado ao testar conexão com banco de dados: {Error}", ex.Message);
    logger.LogError("❌ Inner Exception: {InnerException}", ex.InnerException?.Message);
}

logger.LogInformation("🔍 Teste de conexão concluído.");

// ===== Configuração do Pipeline HTTP =====

// Middleware de logging de requisições (para debug)
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var startTime = DateTime.UtcNow;
    
    try
    {
        await next();
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        if (context.Response.StatusCode >= 500)
        {
            logger.LogWarning("⚠️ Requisição {Method} {Path} retornou {StatusCode} em {Duration}ms", 
                context.Request.Method, context.Request.Path, context.Response.StatusCode, duration);
        }
        else if (duration > 5000) // Logar requisições lentas (>5s)
        {
            logger.LogWarning("🐌 Requisição lenta: {Method} {Path} levou {Duration}ms", 
                context.Request.Method, context.Request.Path, duration);
        }
    }
    catch (Exception ex)
    {
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogError(ex, "❌ Erro não tratado na requisição {Method} {Path} após {Duration}ms: {Error}", 
            context.Request.Method, context.Request.Path, duration, ex.Message);
        throw;
    }
});

// Tratamento global de erros (deve vir primeiro)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        
        logger.LogError(exception, "❌ Erro não tratado: {Error} | Path: {Path} | Method: {Method}", 
            exception?.Message, context.Request.Path, context.Request.Method);
        
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            message = "Erro interno do servidor",
            detail = app.Environment.IsDevelopment() ? exception?.ToString() : null,
            path = context.Request.Path,
            method = context.Request.Method,
            timestamp = DateTime.UtcNow
        };
        
        await context.Response.WriteAsJsonAsync(response);
    });
});

// Swagger sempre habilitado
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Automais IoT Platform API v1");
    c.RoutePrefix = "swagger"; // Swagger em /swagger
});

// Routing deve vir antes dos mapeamentos
app.UseRouting();

// CORS deve vir depois de UseRouting e antes de UseAuthorization
// IMPORTANTE: SignalR precisa de CORS configurado corretamente
app.UseCors("AllowFrontend");

// Mapear endpoints - SignalR deve vir ANTES de MapControllers e UseAuthorization para evitar conflitos
// O endpoint de negociação do SignalR precisa ser acessível sem autenticação
app.MapHub<Automais.Core.Hubs.RouterStatusHub>("/hubs/router-status");

// Authorization (opcional para SignalR, mas necessário para APIs)
app.UseAuthorization();

// Mapear controllers
app.MapControllers();

// Endpoint de tratamento de erros (mantido para compatibilidade, mas o middleware acima já trata)

// Health check robusto
app.MapGet("/health", async (ApplicationDbContext dbContext, ILogger<Program> healthLogger) =>
{
    var healthStatus = new
    {
        status = "healthy",
        mode = "database",
        database = "postgresql (DigitalOcean)",
        chirpstack = chirpStackUrl,
        timestamp = DateTime.UtcNow,
        checks = new Dictionary<string, object>()
    };
    
    // Testar conexão com banco de dados
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        healthStatus.checks["database"] = new
        {
            status = canConnect ? "healthy" : "unhealthy",
            connected = canConnect
        };
        
        if (!canConnect)
        {
            healthLogger.LogWarning("⚠️ Health check: Banco de dados não está acessível");
            return Results.Json(new
            {
                status = "unhealthy",
                checks = healthStatus.checks,
                timestamp = DateTime.UtcNow
            }, statusCode: 503);
        }
        
        // Testar query simples
        var testQuery = await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
        healthStatus.checks["database_query"] = new
        {
            status = "healthy",
            query_executed = true
        };
    }
    catch (Exception ex)
    {
        healthLogger.LogError(ex, "❌ Health check falhou: {Error}", ex.Message);
        healthStatus.checks["database"] = new
        {
            status = "unhealthy",
            error = ex.Message
        };
        return Results.Json(new
        {
            status = "unhealthy",
            checks = healthStatus.checks,
            timestamp = DateTime.UtcNow
        }, statusCode: 503);
    }
    
    return Results.Ok(healthStatus);
});

Console.WriteLine("\n🚀 API rodando!");
if (app.Environment.IsProduction())
{
    Console.WriteLine($"🔒 HTTPS: https://automais.io:5001");
    Console.WriteLine($"📝 Swagger: https://automais.io:5001/swagger");
    Console.WriteLine($"❤️  Health: https://automais.io:5001/health");
}
else
{
    Console.WriteLine($"📝 Swagger: http://localhost:5000/swagger ou https://localhost:5001/swagger");
    Console.WriteLine($"❤️  Health: http://localhost:5000/health");
}
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

