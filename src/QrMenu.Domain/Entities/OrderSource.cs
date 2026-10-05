namespace QrMenu.Domain.Entities;

/// <summary>Who placed the order.</summary>
public enum OrderSource
{
    /// <summary>The guest, from the table's QR menu.</summary>
    Qr,

    /// <summary>Restaurant staff, from the admin panel (counter, phone or walk-in order).</summary>
    Staff
}
