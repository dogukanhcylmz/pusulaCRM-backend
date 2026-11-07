using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PusulaCrm.Api.Dtos;
using PusulaCrm.Api.Services.Ai;

namespace PusulaCrm.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AssistantOptions _options;
    public MeController(IOptions<AssistantOptions> options) => _options = options.Value;

    [HttpGet]
    public CurrentUserDto Get() => new(_options.UserName, _options.UserRole, _options.UserTeam, _options.ProductName);
}
