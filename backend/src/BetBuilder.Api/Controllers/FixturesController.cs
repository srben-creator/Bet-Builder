using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/fixtures")]
[Produces("application/json")]
public class FixturesController : ControllerBase
{
    private readonly IFixturesService _service;

    public FixturesController(IFixturesService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lista os jogos agendados organizados por rodada.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FixtureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FixtureDto>>> GetUpcomingFixtures([FromQuery] string? leagueIds = null, CancellationToken ct = default)
    {
        List<Guid>? parsedLeagueIds = null;
        if (!string.IsNullOrWhiteSpace(leagueIds))
        {
            parsedLeagueIds = leagueIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => Guid.TryParse(id.Trim(), out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
        }

        var result = await _service.GetUpcomingFixturesAsync(parsedLeagueIds, ct);
        return Ok(result);
    }
}
