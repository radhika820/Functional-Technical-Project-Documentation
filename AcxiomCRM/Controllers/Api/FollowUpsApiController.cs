using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/followups")]
public class FollowUpsApiController : ApiBaseController
{
    private readonly IFollowUpService _followUps;
    public FollowUpsApiController(IFollowUpService followUps) => _followUps = followUps;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FollowUpDto>), 200)]
    public async Task<ActionResult<PagedResult<FollowUpDto>>> Get([FromQuery] FollowUpListQuery query) =>
        Ok(ToPage(await _followUps.SearchAsync(query), FollowUpDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _followUps.GetAsync(id);
        return r.Succeeded ? Ok(FollowUpDto.From(r.Value!)) : Fail(r);
    }

    /// <summary>New follow-ups are always created as Planned and cannot be dated before today.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] FollowUpInput input)
    {
        var r = await _followUps.CreateAsync(input);
        if (!r.Succeeded) return Fail(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.FollowUpId }, FollowUpDto.From(r.Value));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] FollowUpInput input)
    {
        var r = await _followUps.UpdateAsync(id, input);
        return r.Succeeded ? Ok(FollowUpDto.From(r.Value!)) : Fail(r);
    }

    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(FollowUpDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteFollowUpInput? input)
    {
        var r = await _followUps.CompleteAsync(id, input?.Remarks);
        return r.Succeeded ? Ok(FollowUpDto.From(r.Value!)) : Fail(r);
    }
}
