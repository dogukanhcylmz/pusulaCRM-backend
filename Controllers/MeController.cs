using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DigitestCrm.Api.Dtos;
using DigitestCrm.Api.Services.Ai;

namespace DigitestCrm.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AssistantOptions _options;
    public MeController(IOptions<AssistantOptions> options) => _options = options.Value;

    [HttpGet]
    public CurrentUserDto Get() => new(_options.UserName, _options.UserRole, _options.UserTeam, _options.ProductName);
}
