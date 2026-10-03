using FluentAssertions;
using QrMenu.Application.Categories;
using QrMenu.Application.Common.Exceptions;
using QrMenu.Domain.Entities;
using QrMenu.Infrastructure.Services;
using QrMenu.Tests.Common;
using Xunit;

namespace QrMenu.Tests.Services;

public class CategoryServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignsNextSortOrder()
    {
        var db = InMemoryDbFactory.Create();
        var restaurantId = Guid.NewGuid();
        db.Categories.Add(new Category { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Starters", SortOrder = 0 });
        await db.SaveChangesAsync();

        var service = new CategoryService(db);
        var result = await service.CreateAsync(restaurantId, new CreateCategoryRequest("Mains"));

        result.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_StartsAtZero_ForFirstCategory()
    {
        var db = InMemoryDbFactory.Create();
        var service = new CategoryService(db);

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateCategoryRequest("Starters"));

        result.SortOrder.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFound_WhenCategoryBelongsToAnotherRestaurant()
    {
        var db = InMemoryDbFactory.Create();
        var ownerRestaurantId = Guid.NewGuid();
        var otherRestaurantId = Guid.NewGuid();
        var category = new Category { Id = Guid.NewGuid(), RestaurantId = otherRestaurantId, Name = "Starters", SortOrder = 0 };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var service = new CategoryService(db);
        var act = async () => await service.UpdateAsync(ownerRestaurantId, category.Id, new UpdateCategoryRequest("Hacked"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFound_WhenCategoryBelongsToAnotherRestaurant()
    {
        var db = InMemoryDbFactory.Create();
        var ownerRestaurantId = Guid.NewGuid();
        var otherRestaurantId = Guid.NewGuid();
        var category = new Category { Id = Guid.NewGuid(), RestaurantId = otherRestaurantId, Name = "Starters", SortOrder = 0 };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var service = new CategoryService(db);
        var act = async () => await service.DeleteAsync(ownerRestaurantId, category.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ReorderAsync_UpdatesSortOrderToMatchGivenSequence()
    {
        var db = InMemoryDbFactory.Create();
        var restaurantId = Guid.NewGuid();
        var first = new Category { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Starters", SortOrder = 0 };
        var second = new Category { Id = Guid.NewGuid(), RestaurantId = restaurantId, Name = "Mains", SortOrder = 1 };
        db.Categories.AddRange(first, second);
        await db.SaveChangesAsync();

        var service = new CategoryService(db);
        await service.ReorderAsync(restaurantId, new ReorderRequest([second.Id, first.Id]));

        var categories = await service.GetAllAsync(restaurantId);
        categories.First(c => c.Id == second.Id).SortOrder.Should().Be(0);
        categories.First(c => c.Id == first.Id).SortOrder.Should().Be(1);
    }
}
