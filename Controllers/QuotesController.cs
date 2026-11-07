using Microsoft.AspNetCore.Mvc;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Models;
using PusulaCrm.Api.Services;
using PusulaCrm.Api.Services.Pdf;

namespace PusulaCrm.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public class QuotesController : ControllerBase
{
    private readonly QuoteService _service;
    private readonly QuotePdfService _pdf;

    public QuotesController(QuoteService service, QuotePdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    [HttpGet]
    public Task<List<QuoteDto>> GetAll([FromQuery] int? customerId, [FromQuery] QuoteStatus? status) =>
        _service.GetAllAsync(customerId, status);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Get(int id) =>
        await _service.GetAsync(id) is { } q ? q : NotFound();

    // pDF'i indirme
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id) =>
        await _pdf.CreateAsync(id) is { } pdf ? File(pdf.Content, "application/pdf", pdf.FileName) : NotFound();

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
