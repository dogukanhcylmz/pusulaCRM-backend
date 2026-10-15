using Microsoft.AspNetCore.Mvc;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Services;

namespace DigitestCrm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _service;
    public DashboardController(DashboardService service) => _service = service;

    [HttpGet]
    public Task<DashboardDto> Get() => _service.GetAsync();
}
