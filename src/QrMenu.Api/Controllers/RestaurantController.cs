using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Restaurants;
using QrMenu.Application.Subscriptions;

namespace QrMenu.Api.Controllers;

[Route("api/restaurant")]
public class RestaurantController(
    IRestaurantService restaurantService,
    ISubscriptionService subscriptionService,
    IOnlinePaymentService onlinePaymentService) : OwnerControllerBase
{
    /// <summary>"My plan": the current subscription, payment history and the plans that can be bought.</summary>
    [HttpGet("plan")]
    public async Task<ActionResult<OwnerPlanDto>> GetPlan(CancellationToken ct)
    {
        return Ok(await subscriptionService.GetOwnerPlanAsync(RestaurantId, ct));
    }

    /// <summary>Starts an online payment for a catalog plan. The plan is applied only after /plan/confirm or the webhook.</summary>
    [HttpPost("plan/checkout")]
    public async Task<ActionResult<CheckoutDto>> StartCheckout(StartCheckoutRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("User id claim is missing from the token."));
        return Ok(await onlinePaymentService.StartCheckoutAsync(RestaurantId, userId, request, ct));
    }

    [HttpPost("plan/confirm")]
    public async Task<ActionResult<OwnerPlanDto>> ConfirmCheckout(ConfirmCheckoutRequest request, CancellationToken ct)
    {
        return Ok(await onlinePaymentService.ConfirmCheckoutAsync(RestaurantId, request, ct));
    }

    [HttpGet]
    public async Task<ActionResult<RestaurantDto>> Get(CancellationToken ct)
    {
        var result = await restaurantService.GetAsync(RestaurantId, ct);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<RestaurantDto>> Update(UpdateRestaurantRequest request, CancellationToken ct)
    {
        var result = await restaurantService.UpdateAsync(RestaurantId, request, ct);
        return Ok(result);
    }

    /// <summary>Sets (or with an empty pin, turns off) the separate kitchen-screen login.</summary>
    [HttpPut("kitchen-pin")]
    public async Task<ActionResult<RestaurantDto>> SetKitchenPin(SetKitchenPinRequest request, CancellationToken ct)
    {
        return Ok(await restaurantService.SetKitchenPinAsync(RestaurantId, request, ct));
    }

    /// <summary>Sets (or with an empty value, removes) the restaurant's own address, e.g. saket.qrenvo.com.</summary>
    [HttpPut("subdomain")]
    public async Task<ActionResult<RestaurantDto>> SetSubdomain(SetSubdomainRequest request, CancellationToken ct)
    {
        return Ok(await restaurantService.SetSubdomainAsync(RestaurantId, request, ct));
    }

    [HttpGet("scan-stats")]
    public async Task<ActionResult<List<ScanStatsDto>>> GetScanStats([FromQuery] int days = 7, CancellationToken ct = default)
    {
        var result = await restaurantService.GetScanStatsAsync(RestaurantId, days, ct);
        return Ok(result);
    }

    [HttpGet("dashboard-stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats(CancellationToken ct)
    {
        var result = await restaurantService.GetDashboardStatsAsync(RestaurantId, ct);
        return Ok(result);
    }
}
