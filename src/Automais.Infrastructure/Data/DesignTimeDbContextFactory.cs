using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Automais.Infrastructure.Data;

/// <summary>
/// Factory para criar ApplicationDbContext em tempo de design (migrations)
/// Permite criar migrations sem precisar do Program.cs completo
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Buscar configuração do appsettings.json
        // O caminho pode variar dependendo de onde o comando é executado
        var basePath = Directory.GetCurrentDirectory();
        
        // Se estiver executando do diretório Infrastructure, subir um nível
        if (basePath.EndsWith("Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            basePath = Path.Combine(basePath, "..");
        }
        
        // Tentar encontrar o diretório Automais.Api
        var apiPath = Path.Combine(basePath, "Automais.Api");
        if (!Directory.Exists(apiPath))
        {
            // Tentar caminho alternativo
            apiPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Automais.Api");
        }
        
        if (!Directory.Exists(apiPath))
        {
            // Última tentativa: usar o diretório atual
            apiPath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Obter connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        // Se não encontrou, tentar construir das variáveis de ambiente
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var host = Environment.GetEnvironmentVariable("DB_HOST") ?? configuration["Database:Host"] ?? "localhost";
            var port = Environment.GetEnvironmentVariable("DB_PORT") ?? configuration["Database:Port"] ?? "5432";
            var database = Environment.GetEnvironmentVariable("DB_NAME") ?? configuration["Database:Name"] ?? "automais";
            var username = Environment.GetEnvironmentVariable("DB_USER") ?? configuration["Database:User"] ?? "postgres";
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? configuration["Database:Password"] ?? "postgres";

            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};Ssl Mode=Require";
        }

        // Substituir variáveis de ambiente no formato ${VAR}
        connectionString = ReplaceEnvironmentVariables(connectionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, opt =>
        {
            opt.EnableRetryOnFailure();
            opt.MapEnumToText();
        });
        // REMOVIDO: UseSnakeCaseNamingConvention() 
        // O banco usa PascalCase (Id, Name, TenantId), não snake_case
        // optionsBuilder.UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ReplaceEnvironmentVariables(string input)
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
}

