using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.SuperAdmins;

namespace QrMenu.Api.Controllers;

// Only tokens with the superAdmin claim get in. Restaurant owner tokens are refused (403).
[ApiController]
[Route("api/superadmin")]
[Authorize(Policy = "SuperAdmin")]
public class SuperAdminController(
    ISuperAdminService superAdminService,
    ISubscriptionService subscriptionService,
    IPricingPlanService pricingPlanService,
    ILogger<SuperAdminController> logger) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<SuperAdminStatsDto>> GetStats(CancellationToken ct)
    {
        return Ok(await superAdminService.GetStatsAsync(ct));
    }

    [HttpGet("restaurants")]
    public async Task<ActionResult<PagedResult<SuperAdminRestaurantDto>>> ListRestaurants(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? plan,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await superAdminService.ListRestaurantsAsync(search, status, plan, page, pageSize, ct));
    }

    [HttpPut("restaurants/{id:guid}/status")]
    public async Task<ActionResult<SuperAdminRestaurantDto>> SetStatus(Guid id, SetRestaurantStatusRequest request, CancellationToken ct)
    {
        var result = await superAdminService.SetRestaurantStatusAsync(id, request.IsActive, ct);
        logger.LogInformation("Super admin {Email} set restaurant {RestaurantId} IsActive={IsActive}",
            User.FindFirst(ClaimTypes.Email)?.Value, id, request.IsActive);
        return Ok(result);
    }

    [HttpGet("restaurants/{id:guid}/subscription")]
    public async Task<ActionResult<SubscriptionDetailsDto>> GetSubscription(Guid id, CancellationToken ct)
    {
        return Ok(await subscriptionService.GetDetailsAsync(id, ct));
    }

    [HttpPost("restaurants/{id:guid}/subscription/free")]
    public async Task<ActionResult<SubscriptionDetailsDto>> GrantFree(Guid id, GrantFreePlanRequest request, CancellationToken ct)
    {
        var result = await subscriptionService.GrantFreeAsync(id, request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} gave restaurant {RestaurantId} a free plan until {Until}",
            AdminEmail, id, result.Current.ExpiresAt?.ToString("u") ?? "lifetime");
        return Ok(result);
    }

    [HttpPost("restaurants/{id:guid}/subscription/payment")]
    public async Task<ActionResult<SubscriptionDetailsDto>> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        var result = await subscriptionService.RecordPaymentAsync(id, request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} recorded {Amount} ({Plan} x{Periods}) for restaurant {RestaurantId}",
            AdminEmail, request.Amount, result.Current.PlanName, request.Periods, id);
        return Ok(result);
    }

    [HttpPost("restaurants/{id:guid}/subscription/extend")]
    public async Task<ActionResult<SubscriptionDetailsDto>> Extend(Guid id, ExtendPlanRequest request, CancellationToken ct)
    {
        var result = await subscriptionService.ExtendAsync(id, request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} extended restaurant {RestaurantId} plan to {Until:u}", AdminEmail, id, request.Until);
        return Ok(result);
    }

    [HttpPost("restaurants/{id:guid}/subscription/cancel")]
    public async Task<ActionResult<SubscriptionDetailsDto>> Cancel(Guid id, CancelPlanRequest request, CancellationToken ct)
    {
        var result = await subscriptionService.CancelAsync(id, request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} cancelled restaurant {RestaurantId} plan", AdminEmail, id);
        return Ok(result);
    }

    // Plan catalog. Owners only ever see the active plans, read-only, on their "My plan" page.

    [HttpGet("plans")]
    public async Task<ActionResult<List<PricingPlanAdminDto>>> GetPlans(CancellationToken ct)
    {
        return Ok(await pricingPlanService.GetAllAsync(ct));
    }

    [HttpPost("plans")]
    public async Task<ActionResult<PricingPlanAdminDto>> CreatePlan(SavePricingPlanRequest request, CancellationToken ct)
    {
        var result = await pricingPlanService.CreateAsync(request, ct);
        logger.LogInformation("Super admin {Email} created plan {PlanId} {Name} at {Price}", AdminEmail, result.Id, result.Name, result.Price);
        return Ok(result);
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<PricingPlanAdminDto>> UpdatePlan(Guid id, SavePricingPlanRequest request, CancellationToken ct)
    {
        var result = await pricingPlanService.UpdateAsync(id, request, ct);
        logger.LogInformation("Super admin {Email} updated plan {PlanId} {Name} at {Price}", AdminEmail, id, result.Name, result.Price);
        return Ok(result);
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        await pricingPlanService.DeleteAsync(id, ct);
        logger.LogInformation("Super admin {Email} deleted plan {PlanId}", AdminEmail, id);
        return NoContent();
    }

    private string AdminEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin";
}
