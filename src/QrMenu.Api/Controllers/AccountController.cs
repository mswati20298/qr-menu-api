using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Auth;

namespace QrMenu.Api.Controllers;

[Route("api/account")]
public class AccountController(IAuthService authService) : OwnerControllerBase
{
    /// <summary>Changes the owner's password. Every other login is signed out; the response carries a new token.</summary>
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPut("password")]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("User id claim is missing from the token."));
        return Ok(await authService.ChangePasswordAsync(userId, request, ct));
    }
}
