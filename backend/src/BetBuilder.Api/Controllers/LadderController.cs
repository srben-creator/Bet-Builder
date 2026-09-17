using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

public record WinStepRequest(decimal Odds);

[ApiController]
[Route("api/ladder")]
[Produces("application/json")]
public class LadderController : ControllerBase
{
    private readonly ILadderService _service;

    public LadderController(ILadderService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retorna o estado atual da Honest Ladder (banca, meta, passo).
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(LadderCurrentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LadderCurrentDto>> GetCurrent(CancellationToken ct = default)
    {
        var result = await _service.GetCurrentChallengeAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Lista seleções com alta probabilidade (>75%) para compor acumuladores.
    /// </summary>
    [HttpGet("safe-legs")]
    [ProducesResponseType(typeof(List<LadderSafeLegDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LadderSafeLegDto>>> GetSafeLegs(CancellationToken ct = default)
    {
        var result = await _service.GetSafeLegsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Registra a vitória do passo atual e calcula o novo saldo de banca.
    /// </summary>
    [HttpPost("step/win")]
    [ProducesResponseType(typeof(LadderCurrentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LadderCurrentDto>> WinStep([FromBody] WinStepRequest request, CancellationToken ct = default)
    {
        var result = await _service.WinStepAsync(request.Odds, ct);
        return Ok(result);
    }

    /// <summary>
    /// Registra a derrota do passo atual, arquivando o desafio.
    /// </summary>
    [HttpPost("step/lose")]
    [ProducesResponseType(typeof(LadderCurrentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LadderCurrentDto>> LoseStep(CancellationToken ct = default)
    {
        var result = await _service.LoseStepAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Reinicia o desafio da Ladder de volta ao passo 1 com aposta inicial.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(LadderCurrentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LadderCurrentDto>> Reset(CancellationToken ct = default)
    {
        var result = await _service.ResetChallengeAsync(ct);
        return Ok(result);
    }
}
