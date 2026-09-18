using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/leagues")]
[Produces("application/json")]
public class LeaguesController : ControllerBase
{
    private readonly ILeagueService _leagueService;

    public LeaguesController(ILeagueService leagueService)
    {
        _leagueService = leagueService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<LeagueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeagueDto>>> GetLeagues(CancellationToken ct = default)
    {
        var leagues = await _leagueService.GetAllLeaguesAsync(ct);
        return Ok(leagues);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeagueDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeagueDto>> UpdateLeague(Guid id, [FromBody] UpdateLeagueDto dto, CancellationToken ct = default)
    {
        var updated = await _leagueService.UpdateLeagueAsync(id, dto, ct);
        return Ok(updated);
    }

    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SeedLeagues(CancellationToken ct = default)
    {
        try {
            await _leagueService.SeedFootballDataLeaguesAsync(ct);
            return NoContent();
        } catch (Exception ex) {
            return StatusCode(500, ex.ToString());
        }
    }

    [HttpPost("merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MergeLeagues(CancellationToken ct = default)
    {
        try {
            await _leagueService.MergeOldLeaguesAsync(ct);
            return NoContent();
        } catch (Exception ex) {
            return StatusCode(500, ex.ToString());
        }
    }
}
