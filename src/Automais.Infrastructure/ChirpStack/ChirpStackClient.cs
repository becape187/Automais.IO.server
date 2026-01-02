using System.Net.Http.Headers;
using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Chirpstack.Api;

namespace Automais.Infrastructure.ChirpStack;

/// <summary>
/// Cliente gRPC para comunicação com o ChirpStack
/// </summary>
public class ChirpStackClient : IChirpStackClient
{
    private readonly string _apiUrl;
    private readonly string _apiToken;
    private readonly ILogger<ChirpStackClient>? _logger;

    public ChirpStackClient(string apiUrl, string apiToken, ILogger<ChirpStackClient>? logger = null)
    {
        _apiUrl = apiUrl;
        _apiToken = apiToken;
        _logger = logger;
    }

    /// <summary>
    /// Cria um canal gRPC com autenticação
    /// </summary>
    private GrpcChannel CreateChannel()
    {
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        var httpClient = new HttpClient(httpHandler);
        
        // Configurar autenticação Bearer Token
        if (!string.IsNullOrEmpty(_apiToken))
        {
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiToken);
        }

        return GrpcChannel.ForAddress(_apiUrl, new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
    }

    /// <summary>
    /// Cria metadata para autenticação
    /// </summary>
    private Metadata CreateMetadata()
    {
        var metadata = new Metadata();
        if (!string.IsNullOrEmpty(_apiToken))
        {
            // ChirpStack aceita "authorization" (minúsculo) no metadata gRPC
            // Vamos tentar ambos para garantir compatibilidade
            metadata.Add("authorization", $"Bearer {_apiToken}");
            // Alguns servidores também aceitam com A maiúsculo
            // metadata.Add("Authorization", $"Bearer {_apiToken}"); // Comentado pois pode duplicar
        }
        else
        {
            _logger?.LogWarning("Tentando criar metadata sem token - autenticação falhará");
        }
        return metadata;
    }

    public async Task<IEnumerable<GatewayDto>> ListGatewaysAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var request = new Api.ListGatewaysRequest
            {
                TenantId = tenantId,
                Limit = 1000
            };

            var metadata = CreateMetadata();
            var response = await client.ListAsync(request, metadata, cancellationToken: cancellationToken);

            return response.Result.Select(g => new GatewayDto
            {
                Name = g.Name,
                GatewayEui = g.GatewayId,
                Description = g.Description ?? string.Empty,
                Latitude = g.Location?.Latitude,
                Longitude = g.Location?.Longitude,
                Altitude = g.Location?.Altitude,
                Status = g.Stats?.LastSeenAt != null 
                    ? (DateTime.UtcNow - g.Stats.LastSeenAt.ToDateTime()).TotalMinutes < 30 
                        ? GatewayStatus.Online 
                        : GatewayStatus.Offline
                    : GatewayStatus.Offline,
                LastSeenAt = g.Stats?.LastSeenAt?.ToDateTime()
            });
            */

            // TEMPORÁRIO: Retorna vazio até ter os .proto
            _logger?.LogWarning("ListGatewaysAsync: Arquivos .proto não configurados. Retornando lista vazia.");
            await Task.CompletedTask;
            return new List<GatewayDto>();
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao listar gateways do ChirpStack: {Status} - {Message}", ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao listar gateways: {ex.Message}", ex);
        }
    }

    public async Task<GatewayDto?> GetGatewayAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var request = new Api.GetGatewayRequest
            {
                GatewayId = gatewayEui
            };

            var metadata = CreateMetadata();
            var response = await client.GetAsync(request, metadata, cancellationToken: cancellationToken);

            if (response.Gateway == null) return null;

            var g = response.Gateway;
            return new GatewayDto
            {
                Name = g.Name,
                GatewayEui = g.GatewayId,
                Description = g.Description ?? string.Empty,
                Latitude = g.Location?.Latitude,
                Longitude = g.Location?.Longitude,
                Altitude = g.Location?.Altitude,
                Status = g.Stats?.LastSeenAt != null 
                    ? (DateTime.UtcNow - g.Stats.LastSeenAt.ToDateTime()).TotalMinutes < 30 
                        ? GatewayStatus.Online 
                        : GatewayStatus.Offline
                    : GatewayStatus.Offline,
                LastSeenAt = g.Stats?.LastSeenAt?.ToDateTime()
            };
            */

            // TEMPORÁRIO
            _logger?.LogWarning("GetGatewayAsync: Arquivos .proto não configurados. Retornando null.");
            await Task.CompletedTask;
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao obter gateway {GatewayEui}: {Status} - {Message}", gatewayEui, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao obter gateway: {ex.Message}", ex);
        }
    }

    public async Task CreateGatewayAsync(CreateGatewayDto gateway, string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var location = gateway.Latitude.HasValue && gateway.Longitude.HasValue
                ? new Api.Common.Location
                {
                    Latitude = gateway.Latitude.Value,
                    Longitude = gateway.Longitude.Value,
                    Altitude = gateway.Altitude ?? 0,
                    Source = Api.Common.LocationSource.Manual
                }
                : null;

            var request = new Api.CreateGatewayRequest
            {
                Gateway = new Api.Gateway
                {
                    GatewayId = gateway.GatewayEui,
                    Name = gateway.Name,
                    Description = gateway.Description ?? string.Empty,
                    TenantId = tenantId,
                    Location = location
                }
            };

            var metadata = CreateMetadata();
            await client.CreateAsync(request, metadata, cancellationToken: cancellationToken);
            
            _logger?.LogInformation("Gateway {GatewayEui} criado no ChirpStack com sucesso", gateway.GatewayEui);
            */

            // TEMPORÁRIO
            _logger?.LogWarning("CreateGatewayAsync: Arquivos .proto não configurados. Operação mockada.");
            Console.WriteLine($"[ChirpStack] Mock: Criando gateway {gateway.Name} ({gateway.GatewayEui}) no tenant {tenantId}");
            await Task.CompletedTask;
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao criar gateway {GatewayEui}: {Status} - {Message}", gateway.GatewayEui, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao criar gateway no ChirpStack: {ex.Message}", ex);
        }
    }

    public async Task UpdateGatewayAsync(string gatewayEui, UpdateGatewayDto gateway, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var location = gateway.Latitude.HasValue && gateway.Longitude.HasValue
                ? new Api.Common.Location
                {
                    Latitude = gateway.Latitude.Value,
                    Longitude = gateway.Longitude.Value,
                    Altitude = gateway.Altitude ?? 0,
                    Source = Api.Common.LocationSource.Manual
                }
                : null;

            var request = new Api.UpdateGatewayRequest
            {
                Gateway = new Api.Gateway
                {
                    GatewayId = gatewayEui,
                    Name = gateway.Name ?? string.Empty,
                    Description = gateway.Description ?? string.Empty,
                    Location = location
                }
            };

            var metadata = CreateMetadata();
            await client.UpdateAsync(request, metadata, cancellationToken: cancellationToken);
            
            _logger?.LogInformation("Gateway {GatewayEui} atualizado no ChirpStack com sucesso", gatewayEui);
            */

            // TEMPORÁRIO
            _logger?.LogWarning("UpdateGatewayAsync: Arquivos .proto não configurados. Operação mockada.");
            Console.WriteLine($"[ChirpStack] Mock: Atualizando gateway {gatewayEui}");
            await Task.CompletedTask;
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao atualizar gateway {GatewayEui}: {Status} - {Message}", gatewayEui, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao atualizar gateway no ChirpStack: {ex.Message}", ex);
        }
    }

    public async Task DeleteGatewayAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var request = new Api.DeleteGatewayRequest
            {
                GatewayId = gatewayEui
            };

            var metadata = CreateMetadata();
            await client.DeleteAsync(request, metadata, cancellationToken: cancellationToken);
            
            _logger?.LogInformation("Gateway {GatewayEui} deletado do ChirpStack com sucesso", gatewayEui);
            */

            // TEMPORÁRIO
            _logger?.LogWarning("DeleteGatewayAsync: Arquivos .proto não configurados. Operação mockada.");
            Console.WriteLine($"[ChirpStack] Mock: Deletando gateway {gatewayEui}");
            await Task.CompletedTask;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger?.LogWarning("Gateway {GatewayEui} não encontrado no ChirpStack", gatewayEui);
            // Não falha se já não existe
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao deletar gateway {GatewayEui}: {Status} - {Message}", gatewayEui, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao deletar gateway no ChirpStack: {ex.Message}", ex);
        }
    }

    public async Task<GatewayStatsDto?> GetGatewayStatsAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.GatewayService.GatewayServiceClient(channel);

            var request = new Api.GetGatewayStatsRequest
            {
                GatewayId = gatewayEui,
                Interval = Api.AggregationInterval.Day
            };

            var metadata = CreateMetadata();
            var response = await client.GetStatsAsync(request, metadata, cancellationToken: cancellationToken);

            if (response.Result == null || response.Result.Count == 0)
                return null;

            var latest = response.Result.OrderByDescending(s => s.Timestamp).First();
            
            return new GatewayStatsDto
            {
                GatewayEui = gatewayEui,
                LastSeenAt = latest.Timestamp?.ToDateTime(),
                MessagesToday = latest.RxPacketsReceived ?? 0,
                SignalStrength = latest.LatestRssi,
                Status = latest.Timestamp != null 
                    ? (DateTime.UtcNow - latest.Timestamp.ToDateTime()).TotalMinutes < 30 ? "online" : "offline"
                    : "offline"
            };
            */

            // TEMPORÁRIO: Mock data
            _logger?.LogWarning("GetGatewayStatsAsync: Arquivos .proto não configurados. Retornando dados mockados.");
            await Task.CompletedTask;
            return new GatewayStatsDto
            {
                GatewayEui = gatewayEui,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-5),
                MessagesToday = Random.Shared.Next(100, 1000),
                SignalStrength = Random.Shared.Next(70, 100),
                Status = "online"
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao obter stats do gateway {GatewayEui}: {Status} - {Message}", gatewayEui, ex.StatusCode, ex.Message);
            return null;
        }
    }

    public async Task<string> CreateChirpStackTenantAsync(string tenantName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiToken))
            {
                _logger?.LogWarning("Token do ChirpStack não configurado. Configure 'ChirpStack:ApiToken' no appsettings.json");
                throw new InvalidOperationException("Token de autenticação do ChirpStack não está configurado. Configure 'ChirpStack:ApiToken' no appsettings.json");
            }

            using var channel = CreateChannel();
            var client = new Chirpstack.Api.TenantService.TenantServiceClient(channel);

            var request = new Chirpstack.Api.CreateTenantRequest
            {
                Tenant = new Chirpstack.Api.Tenant
                {
                    Name = tenantName,
                    CanHaveGateways = true,
                    MaxGatewayCount = 0, // 0 = ilimitado
                    MaxDeviceCount = 0
                }
            };

            var metadata = CreateMetadata();
            _logger?.LogDebug("Criando tenant no ChirpStack: {TenantName} (Token presente: {HasToken})", tenantName, !string.IsNullOrEmpty(_apiToken));
            
            var response = await client.CreateAsync(request, metadata, cancellationToken: cancellationToken);
            
            _logger?.LogInformation("Tenant {TenantName} criado no ChirpStack com ID {TenantId}", tenantName, response.Id);
            return response.Id;
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao criar tenant {TenantName}: {Status} - {Message}", tenantName, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao criar tenant no ChirpStack: {ex.Message}", ex);
        }
    }

    public async Task DeleteChirpStackTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Descomentar quando tiver os arquivos .proto
            /*
            using var channel = CreateChannel();
            var client = new Api.TenantService.TenantServiceClient(channel);

            var request = new Api.DeleteTenantRequest
            {
                Id = tenantId
            };

            var metadata = CreateMetadata();
            await client.DeleteAsync(request, metadata, cancellationToken: cancellationToken);
            
            _logger?.LogInformation("Tenant {TenantId} deletado do ChirpStack com sucesso", tenantId);
            */

            // TEMPORÁRIO
            _logger?.LogWarning("DeleteChirpStackTenantAsync: Arquivos .proto não configurados. Operação mockada.");
            Console.WriteLine($"[ChirpStack] Mock: Deletando tenant {tenantId}");
            await Task.CompletedTask;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger?.LogWarning("Tenant {TenantId} não encontrado no ChirpStack", tenantId);
            // Não falha se já não existe
        }
        catch (RpcException ex)
        {
            _logger?.LogError(ex, "Erro ao deletar tenant {TenantId}: {Status} - {Message}", tenantId, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Erro ao deletar tenant no ChirpStack: {ex.Message}", ex);
        }
    }
}
