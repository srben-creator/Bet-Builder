using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/value-bets")]
[Produces("application/json")]
public class ValueBetsController : ControllerBase
{
    private readonly IValueBetsService _service;

    public ValueBetsController(IValueBetsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retorna as apostas com valor esperado (+EV) com filtros opcionais.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ValueBetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ValueBetDto>>> GetValueBets(
        [FromQuery] double minEdge = 2.0,
        [FromQuery] Guid? leagueId = null,
        [FromQuery] string? date = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetValueBetsAsync(minEdge, leagueId, date, ct);
        return Ok(result);
    }
}
