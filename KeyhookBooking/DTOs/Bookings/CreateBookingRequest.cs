namespace KeyhookBooking.DTOs.Bookings;

/// <summary>
/// Request body for booking an available slot.
/// </summary>
public sealed record CreateBookingRequest
{
    /// <summary>ID of the availability slot to book.</summary>
    /// <example>1</example>
    public int SlotId { get; init; }

    /// <summary>Requested booking date in DD/MM/YYYY format.</summary>
    /// <example>01/01/2099</example>
    public string BookDate { get; init; } = string.Empty;

    /// <summary>ID of the tenant making the booking.</summary>
    /// <example>1</example>
    public int TenantId { get; init; }

    /// <summary>Optional tenant timezone. The slot timezone is used for past-slot validation.</summary>
    /// <example>Pacific/Auckland</example>
    public string? TenantTimeZone { get; init; }
}
