using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/backtest")]
[Produces("application/json")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestService _service;

    public BacktestController(IBacktestService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retorna relatório de simulações históricas e distribuição de CLV.
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(BacktestReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BacktestReportDto>> GetReport(CancellationToken ct = default)
    {
        var result = await _service.GetBacktestReportAsync(ct);
        return Ok(result);
    }
}
