using FluentAssertions;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Application.Subscriptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class PricingPlanServiceTests
{
    private static readonly SavePricingPlanRequest Monthly = new("Monthly", "All features", 1, 499, true, 1);

    [Fact]
    public async Task Create_Update_Delete_Plan()
    {
        var db = InMemoryDbFactory.Create();
        var service = new PricingPlanService(db);

        var created = await service.CreateAsync(Monthly);
        var updated = await service.UpdateAsync(created.Id, Monthly with { Price = 599, IsActive = false });
        await service.DeleteAsync(created.Id);

        created.Price.Should().Be(499);
        updated.Price.Should().Be(599);
        updated.IsActive.Should().BeFalse();
        (await service.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected()
    {
        var service = new PricingPlanService(InMemoryDbFactory.Create());
        await service.CreateAsync(Monthly);

        var act = () => service.CreateAsync(Monthly with { Price = 1 });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Delete_PlanInUse_IsRejected()
    {
        var db = InMemoryDbFactory.Create();
        var service = new PricingPlanService(db);
        var created = await service.CreateAsync(Monthly);
        db.Restaurants.Add(new Restaurant
        {
            Id = Guid.NewGuid(), Name = "R", Slug = "r", WhatsAppNumber = "1",
            Plan = SubscriptionPlan.Paid, PricingPlanId = created.Id
        });
        await db.SaveChangesAsync();

        var act = () => service.DeleteAsync(created.Id);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
