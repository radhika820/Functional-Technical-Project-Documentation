using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
public class CustomersApiController : ApiBaseController
{
    private readonly ICustomerService _customers;
    public CustomersApiController(ICustomerService customers) => _customers = customers;

    /// <summary>List/search customers visible to the caller (q, status, assignedTo, sort, dir, page, pageSize).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), 200)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> Get([FromQuery] ListQuery query) =>
        Ok(ToPage(await _customers.SearchAsync(query), CustomerDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var r = await _customers.GetAsync(id);
        return r.Succeeded ? Ok(CustomerDto.From(r.Value!)) : Fail(r);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CustomerInput input)
    {
        var r = await _customers.CreateAsync(input);
        if (!r.Succeeded) return Fail(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Value!.CustomerId }, CustomerDto.From(r.Value));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerInput input)
    {
        var r = await _customers.UpdateAsync(id, input);
        return r.Succeeded ? Ok(CustomerDto.From(r.Value!)) : Fail(r);
    }

    /// <summary>Deactivates the customer (history is retained). Admin/Manager only.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _customers.DeactivateAsync(id);
        return r.Succeeded ? NoContent() : Fail(r);
    }
}
