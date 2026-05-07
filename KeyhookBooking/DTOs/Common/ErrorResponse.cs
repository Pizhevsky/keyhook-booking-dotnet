namespace KeyhookBooking.DTOs.Common;

/// <summary>
/// Standard error response returned when a request cannot be completed.
/// </summary>
/// <param name="Error">Human-readable error message.</param>
public sealed record ErrorResponse(
    string? Error
);
