namespace QrMenu.Domain.Entities;

public class Restaurant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string WhatsAppNumber { get; set; } = string.Empty;
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.Fixed;
    public string ThemeColor { get; set; } = ThemeColors.Default;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsGstEnabled { get; set; } = true;
    public decimal GstPercentage { get; set; } = 5.00m;
    public bool IsServiceChargeEnabled { get; set; }
    public decimal ServiceChargePercentage { get; set; } = 10.00m;

    public bool ShowWelcomeMessage { get; set; }
    public string? WelcomeMessage { get; set; }

    // Invoices
    /// <summary>GSTIN printed on invoices. Optional.</summary>
    public string? GstNumber { get; set; }
    /// <summary>Invoice numbers look like "{prefix}-0001".</summary>
    public string InvoicePrefix { get; set; } = "INV";

    // Customer UPI payments (money goes straight to the restaurant's own UPI account).
    public string? UpiId { get; set; }
    public string? UpiPayeeName { get; set; }

    // Kitchen display login. Null = no separate kitchen login (the owner can still open the kitchen screen).
    public string? KitchenPinHash { get; set; }
    /// <summary>Goes up whenever the PIN changes, which signs every kitchen screen out.</summary>
    public int KitchenPinVersion { get; set; }

    // Current plan. The full history lives in SubscriptionEvents.
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    /// <summary>The catalog plan bought last (only for Paid).</summary>
    public Guid? PricingPlanId { get; set; }
    /// <summary>Shown to people: "Trial", "Free" or the pricing plan's name at the time it was bought.</summary>
    public string? PlanName { get; set; }
    /// <summary>Null = no end date (lifetime free).</summary>
    public DateTime? PlanExpiresAt { get; set; }
    /// <summary>Set when the super admin cancels the plan: ordering stops at once, no grace period.</summary>
    public DateTime? PlanCancelledAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<ScanLog> ScanLogs { get; set; } = new List<ScanLog>();
    public ICollection<Table> Tables { get; set; } = new List<Table>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<RestaurantBackground> Backgrounds { get; set; } = new List<RestaurantBackground>();
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<SubscriptionEvent> SubscriptionEvents { get; set; } = new List<SubscriptionEvent>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
