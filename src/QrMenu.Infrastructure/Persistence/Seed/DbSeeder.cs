using Microsoft.EntityFrameworkCore;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, bool demoData)
    {
        await db.Database.MigrateAsync();

        if (!demoData || await db.Restaurants.AnyAsync())
        {
            return;
        }

        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = "Saket Rasoi",
            Slug = "saket-rasoi",
            Subdomain = "saket-rasoi",
            Tagline = "Ghar jaisa khaana, dil se pakaya",
            Address = "12, Saket District Centre, New Delhi",
            Phone = "011-29851234",
            WhatsAppNumber = "919876543210",
            OpenTime = new TimeSpan(11, 0, 0),
            CloseTime = new TimeSpan(23, 0, 0),
            LogoUrl = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            IsGstEnabled = true,
            GstPercentage = 5.00m,
            IsServiceChargeEnabled = false,
            ServiceChargePercentage = 10.00m,
            ShowWelcomeMessage = true,
            WelcomeMessage = "Explore our fresh and delicious menu."
        };

        var owner = new User
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            Name = "Ramesh Gupta",
            Email = "owner@saketrasoi.in",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo@123"),
            CreatedAt = DateTime.UtcNow
        };

        var starters = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Starters", SortOrder = 0 };
        var mainCourse = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Main Course", SortOrder = 1 };
        var breads = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Breads & Rice", SortOrder = 2 };
        var desserts = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Desserts", SortOrder = 3 };
        var beverages = new Category { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Name = "Beverages", SortOrder = 4 };

        var paneerTikka = new MenuItem { Id = Guid.NewGuid(), CategoryId = starters.Id, Name = "Paneer Tikka", Description = "Chargrilled cottage cheese marinated in spiced yogurt", Price = 240, IsVeg = true, Tag = "Bestseller", IsAvailable = true, SortOrder = 0 };
        var masalaChai = new MenuItem { Id = Guid.NewGuid(), CategoryId = beverages.Id, Name = "Masala Chai", Description = "Spiced Indian tea brewed with milk", Price = 50, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 0 };

        var items = new List<MenuItem>
        {
            paneerTikka,
            new() { Id = Guid.NewGuid(), CategoryId = starters.Id, Name = "Hara Bhara Kebab", Description = "Spinach and green pea patties with mint chutney", Price = 190, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), CategoryId = starters.Id, Name = "Veg Manchurian", Description = "Crispy vegetable balls tossed in tangy Indo-Chinese sauce", Price = 210, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 2 },

            new() { Id = Guid.NewGuid(), CategoryId = mainCourse.Id, Name = "Paneer Butter Masala", Description = "Cottage cheese in a rich tomato and butter gravy", Price = 280, IsVeg = true, Tag = "Bestseller", IsAvailable = true, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), CategoryId = mainCourse.Id, Name = "Dal Makhani", Description = "Slow-cooked black lentils finished with cream", Price = 220, IsVeg = true, Tag = "Bestseller", IsAvailable = true, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), CategoryId = mainCourse.Id, Name = "Jain Kadhai Sabzi", Description = "Mixed vegetables in a spiced kadhai gravy, no onion-garlic", Price = 230, IsVeg = true, Tag = "Jain option", IsAvailable = true, SortOrder = 2 },
            new() { Id = Guid.NewGuid(), CategoryId = mainCourse.Id, Name = "Malai Kofta", Description = "Fried paneer dumplings in a creamy cashew gravy", Price = 260, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 3 },

            new() { Id = Guid.NewGuid(), CategoryId = breads.Id, Name = "Butter Naan", Description = "Tandoor-baked leavened bread brushed with butter", Price = 60, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), CategoryId = breads.Id, Name = "Jeera Rice", Description = "Basmati rice tempered with cumin", Price = 170, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 1 },

            new() { Id = Guid.NewGuid(), CategoryId = desserts.Id, Name = "Gulab Jamun", Description = "Soft milk dumplings soaked in rose-cardamom syrup", Price = 110, IsVeg = true, Tag = null, IsAvailable = true, SortOrder = 0 },

            masalaChai
        };

        var variants = new List<ItemVariant>
        {
            new() { Id = Guid.NewGuid(), MenuItemId = masalaChai.Id, Name = "Regular", Price = 50, IsDefault = true, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), MenuItemId = masalaChai.Id, Name = "Large", Price = 80, IsDefault = false, SortOrder = 1 }
        };

        var addOns = new List<ItemAddOn>
        {
            new() { Id = Guid.NewGuid(), MenuItemId = paneerTikka.Id, Name = "Extra Mint Chutney", Price = 20, SortOrder = 0 },
            new() { Id = Guid.NewGuid(), MenuItemId = paneerTikka.Id, Name = "Extra Cheese", Price = 40, SortOrder = 1 }
        };

        var tables = Enumerable.Range(1, 8)
            .Select(n => new Table { Id = Guid.NewGuid(), RestaurantId = restaurant.Id, Number = n.ToString(), Capacity = n % 2 == 0 ? 4 : 2, IsActive = true })
            .ToList();

        db.Restaurants.Add(restaurant);
        db.Users.Add(owner);
        db.Categories.AddRange(starters, mainCourse, breads, desserts, beverages);
        db.MenuItems.AddRange(items);
        db.ItemVariants.AddRange(variants);
        db.ItemAddOns.AddRange(addOns);
        db.Tables.AddRange(tables);

        await db.SaveChangesAsync();

        var demoOrders = new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                TableId = tables[3].Id,
                TableNumberSnapshot = tables[3].Number,
                CustomerName = "Aarav",
                CustomerPhone = "9811122233",
                Status = OrderStatus.Served,
                Subtotal = 420,
                ServiceChargeAmount = 0,
                GstAmount = 21,
                Total = 441,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                UpdatedAt = DateTime.UtcNow.AddHours(-1),
                Items =
                [
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = paneerTikka.Id, ItemName = "Paneer Tikka", UnitPrice = 240, Qty = 1, LineTotal = 240 },
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = masalaChai.Id, ItemName = "Masala Chai", VariantName = "Large", UnitPrice = 80, Qty = 2, LineTotal = 160 }
                ]
            },
            new()
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                TableId = tables[1].Id,
                TableNumberSnapshot = tables[1].Number,
                CustomerName = "Priya",
                CustomerPhone = "9822233344",
                Status = OrderStatus.Preparing,
                Subtotal = 380,
                ServiceChargeAmount = 0,
                GstAmount = 19,
                Total = 399,
                CreatedAt = DateTime.UtcNow.AddMinutes(-25),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                Items =
                [
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = null, ItemName = "Malai Kofta", UnitPrice = 260, Qty = 1, LineTotal = 260 },
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = null, ItemName = "Butter Naan", UnitPrice = 60, Qty = 2, LineTotal = 120 }
                ]
            },
            new()
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                TableId = tables[6].Id,
                TableNumberSnapshot = tables[6].Number,
                CustomerName = "Rohan",
                CustomerPhone = "9833344455",
                Status = OrderStatus.Placed,
                Subtotal = 290,
                ServiceChargeAmount = 0,
                GstAmount = 14.5m,
                Total = 304.5m,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                Items =
                [
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = null, ItemName = "Dal Makhani", UnitPrice = 220, Qty = 1, LineTotal = 220 },
                    new OrderItem { Id = Guid.NewGuid(), MenuItemId = null, ItemName = "Jeera Rice", UnitPrice = 70, Qty = 1, LineTotal = 70 }
                ]
            }
        };

        db.Orders.AddRange(demoOrders);
        await db.SaveChangesAsync();
    }
}
