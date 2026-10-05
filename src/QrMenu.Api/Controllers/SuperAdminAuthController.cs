using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.SuperAdmins;

namespace QrMenu.Api.Controllers;

[ApiController]
[Route("api/superadmin/auth")]
public class SuperAdminAuthController(ISuperAdminService superAdminService) : ControllerBase
{
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<SuperAdminAuthResponse>> Login(SuperAdminLoginRequest request, CancellationToken ct)
    {
        var result = await superAdminService.LoginAsync(request, ct);
        return Ok(result);
    }
}
