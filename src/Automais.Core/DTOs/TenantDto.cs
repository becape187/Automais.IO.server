using Automais.Core.Entities;

namespace Automais.Core.DTOs;

/// <summary>
/// DTO para retorno de Tenant
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public string? ChirpStackTenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO para criação de Tenant
/// </summary>
public class CreateTenantDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

/// <summary>
/// DTO para atualização de Tenant
/// </summary>
public class UpdateTenantDto
{
    public string? Name { get; set; }
    public TenantStatus? Status { get; set; }
}

