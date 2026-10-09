using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.SuperAdmins;

namespace QrMenu.Api.Controllers;

[ApiController]
[Route("api/superadmin/auth")]
public class SuperAdminAuthController(ISuperAdminService superAdminService, ISuperAdminTwoFactorService twoFactorService) : ControllerBase
{
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<SuperAdminAuthResponse>> Login(SuperAdminLoginRequest request, CancellationToken ct)
    {
        var result = await superAdminService.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Second step: the 6-digit code from the authenticator app (or a recovery code).</summary>
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("verify-2fa")]
    public async Task<ActionResult<SuperAdminAuthResponse>> VerifyTwoFactor(VerifyTwoFactorRequest request, CancellationToken ct)
    {
        return Ok(await twoFactorService.VerifyLoginAsync(request, ct));
    }
}
