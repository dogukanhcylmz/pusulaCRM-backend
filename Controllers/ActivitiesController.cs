using Microsoft.AspNetCore.Mvc;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Services;

namespace DigitestCrm.Api.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController : ControllerBase
{
    private readonly ActivityService _service;
    public ActivitiesController(ActivityService service) => _service = service;

    [HttpGet]
    public Task<List<ActivityDto>> GetAll([FromQuery] int? customerId, [FromQuery] bool? completed) =>
        _service.GetAllAsync(customerId, completed);

    [HttpPost]
    public async Task<ActionResult<ActivityDto>> Create(ActivitySaveDto dto) =>
        Created($"api/activities", await _service.CreateAsync(dto));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ActivityDto>> Update(int id, ActivityUpdateDto dto) =>
        await _service.UpdateAsync(id, dto) is { } a ? a : NotFound();

    [HttpPatch("{id:int}/complete")]
    public async Task<ActionResult<ActivityDto>> Complete(int id, [FromQuery] bool value = true) =>
        await _service.SetCompletedAsync(id, value) is { } a ? a : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
