using Microsoft.AspNetCore.Mvc;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Models;
using PusulaCrm.Api.Services;

namespace PusulaCrm.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _service;
    public CustomersController(CustomerService service) => _service = service;

    [HttpGet]
    public Task<List<CustomerDto>> GetAll([FromQuery] string? search, [FromQuery] CustomerStatus? status) =>
        _service.GetAllAsync(search, status);

    [HttpGet("paged")]
    public Task<PagedResult<CustomerDto>> GetPaged([FromQuery] string? search, [FromQuery] CustomerStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10) =>
        _service.GetPagedAsync(search, status, page, pageSize);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Get(int id) =>
        await _service.GetAsync(id) is { } c ? c : NotFound();

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CustomerSaveDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerSaveDto dto) =>
        await _service.UpdateAsync(id, dto) is { } c ? c : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
