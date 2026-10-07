using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/reports")]
public class ReportsApiController : ApiBaseController
{
    private readonly IReportService _reports;
    public ReportsApiController(IReportService reports) => _reports = reports;

    /// <summary>Stage-wise and owner-wise pipeline data, limited to the caller's authorized scope.</summary>
    [HttpGet("pipeline")]
    [ProducesResponseType(typeof(PipelineReportDto), 200)]
    public async Task<ActionResult<PipelineReportDto>> Pipeline() => Ok(await _reports.PipelineDataAsync());
}
