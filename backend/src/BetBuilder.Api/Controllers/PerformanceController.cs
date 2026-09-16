using BetBuilder.Application.DTOs;
using BetBuilder.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetBuilder.Api.Controllers;

[ApiController]
[Route("api/performance")]
[Produces("application/json")]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceService _service;

    public PerformanceController(IPerformanceService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retorna métricas consolidadas de ROI, Win Rate e evolução de banca.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PerformanceDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PerformanceDashboardDto>> GetDashboard(CancellationToken ct = default)
    {
        var result = await _service.GetPerformanceDashboardAsync(ct);
        return Ok(result);
    }
}
