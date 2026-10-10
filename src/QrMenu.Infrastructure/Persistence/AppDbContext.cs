using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using QrMenu.Domain.Entities;

namespace QrMenu.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ScanLog> ScanLogs => Set<ScanLog>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();
    public DbSet<ItemAddOn> ItemAddOns => Set<ItemAddOn>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<RestaurantBackground> RestaurantBackgrounds => Set<RestaurantBackground>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<PlanPayment> PlanPayments => Set<PlanPayment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<PaymentGatewayLog> PaymentGatewayLogs => Set<PaymentGatewayLog>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<PlanRefund> PlanRefunds => Set<PlanRefund>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQL Server's datetime2 columns carry no timezone info, so EF Core reads every
        // DateTime back with Kind=Unspecified — even though we always write DateTime.UtcNow.
        // That makes System.Text.Json omit the "Z" suffix, and browsers then read the
        // timestamp as local time instead of UTC (an ~5:30h drift for IST). Marking every
        // DateTime as Utc on read fixes the serialization for every entity at once.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }
}
