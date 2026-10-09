using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrMenu.Application.Feedbacks;
using QrMenu.Application.Platform;
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
    IPlatformSettingsService platformSettingsService,
    IPlatformKeysService platformKeysService,
    IDemoResetService demoResetService,
    IFeedbackService feedbackService,
    ISuperAdminTwoFactorService twoFactorService,
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

    [HttpPost("restaurants/{id:guid}/reset-password")]
    public async Task<ActionResult<ResetOwnerPasswordResponse>> ResetOwnerPassword(Guid id, CancellationToken ct)
    {
        var result = await superAdminService.ResetOwnerPasswordAsync(id, ct);
        logger.LogInformation("Super admin {Email} reset the owner password of restaurant {RestaurantId}",
            User.FindFirst(ClaimTypes.Email)?.Value, id);
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

    // Every plan payment (Razorpay checkouts and payments recorded by hand). Super admin only.

    [HttpGet("payments")]
    public async Task<ActionResult<PaymentLogResponse>> Payments([FromQuery] string? status, [FromQuery] string? search, CancellationToken ct)
    {
        return Ok(await superAdminService.ListPaymentsAsync(status, search, ct));
    }

    [HttpGet("payments/gateway-log")]
    public async Task<ActionResult<List<PaymentGatewayLogDto>>> GatewayLog([FromQuery] string orderId, CancellationToken ct)
    {
        return Ok(await superAdminService.GetGatewayLogAsync(orderId, ct));
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

    // Platform settings (free-trial length for new restaurants).

    [HttpGet("settings")]
    public async Task<ActionResult<PlatformSettingsDto>> GetSettings(CancellationToken ct)
    {
        return Ok(await platformSettingsService.GetAsync(ct));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<PlatformSettingsDto>> UpdateSettings(UpdatePlatformSettingsRequest request, CancellationToken ct)
    {
        var result = await platformSettingsService.UpdateAsync(request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} set the free trial to {TrialDays} days", AdminEmail, result.TrialDays);
        return Ok(result);
    }

    // Payment and AI keys. Secrets go in, but never come back out (only "set", the source and the last 4 characters).

    [HttpGet("settings/keys")]
    public async Task<ActionResult<PlatformKeysDto>> GetKeys(CancellationToken ct)
    {
        return Ok(await platformKeysService.GetAsync(ct));
    }

    [HttpPut("settings/keys")]
    public async Task<ActionResult<PlatformKeysDto>> UpdateKeys(UpdatePlatformKeysRequest request, CancellationToken ct)
    {
        var result = await platformKeysService.UpdateAsync(request, AdminEmail, ct);
        var changed = new[]
        {
            request.RazorpayKeyId is null ? null : "Razorpay key id",
            request.RazorpayKeySecret is null ? null : "Razorpay key secret",
            request.RazorpayWebhookSecret is null ? null : "Razorpay webhook secret",
            request.GeminiApiKey is null ? null : "Gemini API key"
        }.Where(k => k is not null);
        logger.LogInformation("Super admin {Email} changed: {Keys}", AdminEmail, string.Join(", ", changed));
        return Ok(result);
    }

    [HttpPost("settings/keys/test-razorpay")]
    public async Task<ActionResult<KeyCheckResultDto>> TestRazorpay(TestRazorpayKeysRequest request, CancellationToken ct)
    {
        return Ok(await platformKeysService.TestRazorpayAsync(request, ct));
    }

    // Demo deployment: wipe and re-seed the sample data, by hand or every N days.

    [HttpGet("demo")]
    public async Task<ActionResult<DemoStatusDto>> GetDemo(CancellationToken ct)
    {
        return Ok(await demoResetService.GetStatusAsync(ct));
    }

    [HttpPut("demo")]
    public async Task<ActionResult<DemoStatusDto>> UpdateDemo(UpdateDemoSettingsRequest request, CancellationToken ct)
    {
        var result = await demoResetService.UpdateSettingsAsync(request, AdminEmail, ct);
        logger.LogInformation("Super admin {Email} set the demo auto reset to {Days} days", AdminEmail, request.AutoResetDays);
        return Ok(result);
    }

    [HttpPost("demo/reset")]
    public async Task<ActionResult<DemoStatusDto>> ResetDemo(CancellationToken ct)
    {
        logger.LogInformation("Super admin {Email} is resetting the demo data", AdminEmail);
        return Ok(await demoResetService.ResetAsync(AdminEmail, ct));
    }

    // Ratings from guests and owners; published ones appear on the landing page.

    [HttpGet("feedback")]
    public async Task<ActionResult<List<FeedbackDto>>> ListFeedback([FromQuery] string? kind, CancellationToken ct)
    {
        return Ok(await feedbackService.ListAllAsync(kind, ct));
    }

    [HttpPut("feedback/{id:guid}/publish")]
    public async Task<ActionResult<FeedbackDto>> PublishFeedback(Guid id, SetFeedbackPublishedRequest request, CancellationToken ct)
    {
        var result = await feedbackService.SetPublishedAsync(id, request.IsPublished, ct);
        logger.LogInformation("Super admin {Email} set feedback {Id} published={Published}", AdminEmail, id, request.IsPublished);
        return Ok(result);
    }

    [HttpDelete("feedback/{id:guid}")]
    public async Task<IActionResult> DeleteFeedback(Guid id, CancellationToken ct)
    {
        await feedbackService.DeleteAsync(id, ct);
        logger.LogInformation("Super admin {Email} deleted feedback {Id}", AdminEmail, id);
        return NoContent();
    }

    // Two-step login for the signed-in super admin.

    [HttpGet("2fa")]
    public async Task<ActionResult<TwoFactorStatusDto>> TwoFactorStatus(CancellationToken ct)
    {
        return Ok(await twoFactorService.GetStatusAsync(AdminId, ct));
    }

    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupDto>> TwoFactorSetup(CancellationToken ct)
    {
        return Ok(await twoFactorService.StartSetupAsync(AdminId, ct));
    }

    [HttpPost("2fa/enable")]
    public async Task<ActionResult<TwoFactorEnabledDto>> TwoFactorEnable(EnableTwoFactorRequest request, CancellationToken ct)
    {
        var result = await twoFactorService.EnableAsync(AdminId, request, ct);
        logger.LogInformation("Super admin {Email} turned on two-step login", AdminEmail);
        return Ok(result);
    }

    [HttpPost("2fa/recovery-codes")]
    public async Task<ActionResult<TwoFactorEnabledDto>> TwoFactorRecoveryCodes(EnableTwoFactorRequest request, CancellationToken ct)
    {
        var result = await twoFactorService.RegenerateRecoveryCodesAsync(AdminId, request, ct);
        logger.LogInformation("Super admin {Email} made new recovery codes", AdminEmail);
        return Ok(result);
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> TwoFactorDisable(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await twoFactorService.DisableAsync(AdminId, request, ct);
        logger.LogInformation("Super admin {Email} turned off two-step login", AdminEmail);
        return NoContent();
    }

    private Guid AdminId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string AdminEmail => User.FindFirst(ClaimTypes.Email)?.Value ?? "superadmin";
}
