using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
public class OpportunitiesApiController : ApiBaseController
{
    private readonly IOpportunityService _opps;
    public OpportunitiesApiController(IOpportunityService opps) => _opps = opps;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OpportunityDto>), 200)]
    public async Task<ActionResult<PagedResult<OpportunityDto>>> Get([FromQuery] OpportunityListQuery query) =>
        Ok(ToPage(await _opps.SearchAsync(query), OpportunityDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _opps.GetAsync(id);
        return r.Succeeded ? Ok(OpportunityDto.From(r.Value!)) : Fail(r);
    }

    /// <summary>Amount must be &gt; 0, probability 0-100, and the close date cannot be in the past for an active opportunity.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] OpportunityInput input)
    {
        var r = await _opps.CreateAsync(input);
        if (!r.Succeeded) return Fail(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.OpportunityId }, OpportunityDto.From(r.Value));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] OpportunityInput input)
    {
        var r = await _opps.UpdateAsync(id, input);
        return r.Succeeded ? Ok(OpportunityDto.From(r.Value!)) : Fail(r);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _opps.DeleteAsync(id);
        return r.Succeeded ? NoContent() : Fail(r);
    }
}
