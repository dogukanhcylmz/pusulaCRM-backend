using Microsoft.AspNetCore.Mvc;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Services;

namespace PusulaCrm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _service;
    public DashboardController(DashboardService service) => _service = service;

    [HttpGet]
    public Task<DashboardDto> Get() => _service.GetAsync();
}
