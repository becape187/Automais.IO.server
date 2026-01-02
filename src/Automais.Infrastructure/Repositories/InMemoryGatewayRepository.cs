using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação em memória do repositório de Gateways (sem banco de dados)
/// </summary>
public class InMemoryGatewayRepository : IGatewayRepository
{
    private readonly List<Gateway> _gateways = new();
    private readonly object _lock = new();

    public Task<Gateway?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var gateway = _gateways.FirstOrDefault(g => g.Id == id);
            return Task.FromResult(gateway);
        }
    }

    public Task<Gateway?> GetByEuiAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var gateway = _gateways.FirstOrDefault(g => 
                g.GatewayEui.Equals(gatewayEui, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(gateway);
        }
    }

    public Task<IEnumerable<Gateway>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _gateways
                .Where(g => g.TenantId == tenantId)
                .OrderBy(g => g.Name)
                .ToList();
            return Task.FromResult<IEnumerable<Gateway>>(result);
        }
    }

    public Task<IEnumerable<Gateway>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _gateways.OrderBy(g => g.Name).ToList();
            return Task.FromResult<IEnumerable<Gateway>>(result);
        }
    }

    public Task<Gateway> CreateAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _gateways.Add(gateway);
            return Task.FromResult(gateway);
        }
    }

    public Task<Gateway> UpdateAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var existing = _gateways.FirstOrDefault(g => g.Id == gateway.Id);
            if (existing != null)
            {
                _gateways.Remove(existing);
                _gateways.Add(gateway);
            }
            return Task.FromResult(gateway);
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var gateway = _gateways.FirstOrDefault(g => g.Id == id);
            if (gateway != null)
            {
                _gateways.Remove(gateway);
            }
            return Task.CompletedTask;
        }
    }

    public Task<bool> EuiExistsAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exists = _gateways.Any(g => 
                g.GatewayEui.Equals(gatewayEui, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(exists);
        }
    }
}

