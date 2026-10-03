namespace QrMenu.Domain.Entities;

/// <summary>Platform administrator. Not tied to any restaurant; can only use the /api/superadmin endpoints.</summary>
public class SuperAdmin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
