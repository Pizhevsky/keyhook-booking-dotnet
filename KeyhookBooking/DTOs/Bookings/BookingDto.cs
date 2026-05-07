namespace KeyhookBooking.DTOs.Bookings;

/// <summary>
/// Booking returned by the API.
/// </summary>
/// <param name="Id">Booking ID.</param>
/// <param name="SlotId">Booked availability slot ID.</param>
/// <param name="TenantId">Tenant user ID.</param>
/// <param name="BookDate">Booked date in DD/MM/YYYY format.</param>
/// <param name="CreatedAt">UTC timestamp when the booking was created.</param>
/// <param name="Status">Booking status: active, cancelled_by_tenant, or cancelled_by_manager.</param>
/// <param name="CancelledAt">UTC timestamp when the booking was cancelled, if any.</param>
public sealed record BookingDto(
    int Id,
    int SlotId,
    int TenantId,
    string BookDate,
    DateTime CreatedAt,
    string Status,
    DateTime? CancelledAt
);
