namespace KeyhookBooking.DTOs.Bookings;

/// <summary>
/// Legacy request model for cancellation payloads.
/// </summary>
/// <param name="CancelledBy">ID of the user cancelling the booking.</param>
public sealed record CancelBookingRequest(
    string CancelledBy
);
