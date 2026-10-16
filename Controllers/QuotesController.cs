using Microsoft.AspNetCore.Mvc;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Models;
using DigitestCrm.Api.Services;

namespace DigitestCrm.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public class QuotesController : ControllerBase
{
    private readonly QuoteService _service;
    public QuotesController(QuoteService service) => _service = service;

    [HttpGet]
    public Task<List<QuoteDto>> GetAll([FromQuery] int? customerId, [FromQuery] QuoteStatus? status) =>
        _service.GetAllAsync(customerId, status);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Get(int id) =>
        await _service.GetAsync(id) is { } q ? q : NotFound();

    [HttpPost]
    public async Task<ActionResult<QuoteDto>> Create(QuoteSaveDto dto)
    {
        var created = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Update(int id, QuoteUpdateDto dto) =>
        await _service.UpdateAsync(id, dto) is { } q ? q : NotFound();

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<QuoteDto>> UpdateStatus(int id, QuoteStatusDto dto) =>
        await _service.UpdateStatusAsync(id, dto.Status) is { } q ? q : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
