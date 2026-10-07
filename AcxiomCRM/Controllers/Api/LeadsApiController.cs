using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
public class LeadsApiController : ApiBaseController
{
    private readonly ILeadService _leads;
    public LeadsApiController(ILeadService leads) => _leads = leads;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeadDto>), 200)]
    public async Task<ActionResult<PagedResult<LeadDto>>> Get([FromQuery] ListQuery query) =>
        Ok(ToPage(await _leads.SearchAsync(query), LeadDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _leads.GetAsync(id);
        return r.Succeeded ? Ok(LeadDto.From(r.Value!)) : Fail(r);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] LeadInput input)
    {
        var r = await _leads.CreateAsync(input);
        if (!r.Succeeded) return Fail(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.LeadId }, LeadDto.From(r.Value));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] LeadInput input)
    {
        var r = await _leads.UpdateAsync(id, input);
        return r.Succeeded ? Ok(LeadDto.From(r.Value!)) : Fail(r);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _leads.DeleteAsync(id);
        return r.Succeeded ? NoContent() : Fail(r);
    }
}
