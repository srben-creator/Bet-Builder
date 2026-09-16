using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/sync")]
[Produces("application/json")]
public class SyncController : ControllerBase
{
    private readonly IDataSyncService _syncService;

    public SyncController(IDataSyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Dispara busca de jogos e cotações na The Odds API, roda Dixon-Coles e calcula +EV.
    /// </summary>
    [HttpPost("live-odds")]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResultDto>> SyncLiveOdds(CancellationToken ct = default)
    {
        var result = await _syncService.SyncLiveOddsAndPredictAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Baixa resultados recentes no football-data.co.uk e liquida apostas pendentes.
    /// </summary>
    [HttpPost("settle")]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResultDto>> SettleResults(CancellationToken ct = default)
    {
        var result = await _syncService.SyncWeekendResultsAndSettleAsync(ct);
        return Ok(result);
    }
}
