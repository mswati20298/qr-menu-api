using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QrMenu.Application.Auth;
using QrMenu.Application.Backgrounds;
using QrMenu.Application.Categories;
using QrMenu.Application.Common;
using QrMenu.Application.Common.Interfaces;
using QrMenu.Application.Feedbacks;
using QrMenu.Application.Invoices;
using QrMenu.Application.Items;
using QrMenu.Application.Kitchen;
using QrMenu.Application.Orders;
using QrMenu.Application.Platform;
using QrMenu.Application.PublicMenu;
using QrMenu.Application.Restaurants;
using QrMenu.Application.ServiceRequests;
using QrMenu.Application.SuperAdmins;
using QrMenu.Application.Subscriptions;
using QrMenu.Application.Tables;
using QrMenu.Infrastructure.Auth;
using QrMenu.Infrastructure.Files;
using QrMenu.Infrastructure.Payments;
using QrMenu.Infrastructure.Pdf;
using QrMenu.Infrastructure.Persistence;
using QrMenu.Infrastructure.Security;
using QrMenu.Infrastructure.Services;

namespace QrMenu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<SiteSettings>(configuration.GetSection("Site"));
        services.Configure<SubscriptionSettings>(configuration.GetSection("Subscription"));
        services.AddSingleton(TimeProvider.System);
        services.Configure<RazorpaySettings>(configuration.GetSection("Razorpay"));
        services.AddSingleton<SecretProtector>();
        services.AddSingleton<IntegrationKeyStore>();
        services.AddSingleton<PaymentGatewayLogWriter>();
        services.AddHttpClient<IPaymentGateway, RazorpayGateway>(client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITableSessionTokens, TableSessionTokens>();
        services.AddScoped<TableAccessGuard>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IImageOptimizer, ImageOptimizer>();
        services.AddScoped<IQrPdfService, QrCardPdfService>();
        services.AddScoped<IInvoicePdfService, InvoicePdfService>();
        services.AddHttpClient<IMenuScanService, GeminiMenuScanService>(client => client.Timeout = TimeSpan.FromSeconds(120));

        services.AddScoped<IPlatformKeysService, PlatformKeysService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddSingleton<TwoFactorChallenges>();
        services.AddScoped<ISuperAdminTwoFactorService, SuperAdminTwoFactorService>();
        services.AddScoped<IDemoResetService, DemoResetService>();
        services.AddHostedService<DemoAutoResetWorker>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IRestaurantBackgroundService, RestaurantBackgroundService>();
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<ISuperAdminService, SuperAdminService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPricingPlanService, PricingPlanService>();
        services.AddScoped<IOnlinePaymentService, OnlinePaymentService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();

        return services;
    }
}
