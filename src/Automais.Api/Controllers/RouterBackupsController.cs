using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class RouterBackupsController : ControllerBase
{
    private readonly IRouterBackupService _backupService;
    private readonly ILogger<RouterBackupsController> _logger;

    public RouterBackupsController(
        IRouterBackupService backupService,
        ILogger<RouterBackupsController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    [HttpGet("routers/{routerId:guid}/backups")]
    public async Task<ActionResult<IEnumerable<RouterBackupDto>>> GetBackups(Guid routerId, CancellationToken cancellationToken)
    {
        var backups = await _backupService.GetByRouterIdAsync(routerId, cancellationToken);
        return Ok(backups);
    }

    [HttpGet("backups/{id:guid}")]
    public async Task<ActionResult<RouterBackupDto>> GetBackupById(Guid id, CancellationToken cancellationToken)
    {
        var backup = await _backupService.GetByIdAsync(id, cancellationToken);
        if (backup == null)
        {
            return NotFound(new { message = $"Backup com ID {id} não encontrado" });
        }
        return Ok(backup);
    }

    [HttpPost("routers/{routerId:guid}/backups")]
    public async Task<ActionResult<RouterBackupDto>> CreateBackup(Guid routerId, [FromBody] CreateRouterBackupDto dto, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Obter userId do contexto de autenticação
            var created = await _backupService.CreateBackupAsync(routerId, dto, null, cancellationToken);
            return CreatedAtAction(nameof(GetBackupById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado ao criar backup");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar backup");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("backups/{id:guid}")]
    public async Task<IActionResult> DeleteBackup(Guid id, CancellationToken cancellationToken)
    {
        await _backupService.DeleteBackupAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("backups/{id:guid}/download")]
    public async Task<IActionResult> DownloadBackup(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var backup = await _backupService.GetByIdAsync(id, cancellationToken);
            if (backup == null)
            {
                return NotFound(new { message = $"Backup com ID {id} não encontrado" });
            }

            var fileBytes = await _backupService.DownloadBackupAsync(id, cancellationToken);
            return File(fileBytes, "application/octet-stream", backup.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Backup não encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Arquivo de backup não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("backups/{id:guid}/content")]
    public async Task<ActionResult<string>> GetBackupContent(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _backupService.GetBackupContentAsync(id, cancellationToken);
            return Ok(new { content });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Backup não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("routers/{routerId:guid}/backups/{backupId:guid}/compare")]
    public async Task<ActionResult<RouterBackupComparisonDto>> CompareBackup(Guid routerId, Guid backupId, CancellationToken cancellationToken)
    {
        try
        {
            var comparison = await _backupService.CompareBackupAsync(routerId, backupId, cancellationToken);
            return Ok(comparison);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router ou backup não encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao comparar backup");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("routers/{routerId:guid}/backups/{backupId:guid}/restore")]
    public async Task<IActionResult> RestoreBackup(Guid routerId, Guid backupId, [FromBody] RestoreRouterBackupDto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _backupService.RestoreBackupAsync(routerId, backupId, dto, cancellationToken);
            return Ok(new { message = dto.DryRun ? "Validação concluída" : "Backup restaurado com sucesso" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router ou backup não encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao restaurar backup");
            return BadRequest(new { message = ex.Message });
        }
    }
}

