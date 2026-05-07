namespace KeyhookBooking.DTOs.Availability;

/// <summary>
/// Request body for updating an availability slot. Omitted fields keep their current values.
/// </summary>
/// <param name="ManagerId">ID of the manager who owns the slot.</param>
/// <param name="SelectedDate">Optional single date in DD/MM/YYYY format.</param>
/// <param name="DaysOfWeek">Optional recurring ISO weekdays, 1 for Monday through 7 for Sunday, separated by semicolons.</param>
/// <param name="StartTime">Optional slot start time in HH:mm format.</param>
/// <param name="EndTime">Optional slot end time in HH:mm format.</param>
/// <param name="TimeZone">Optional IANA timezone used when evaluating the slot time.</param>
public sealed record UpdateAvailabilityRequest(
    int? ManagerId,
    string? SelectedDate,
    string? DaysOfWeek,
    string? StartTime,
    string? EndTime,
    string? TimeZone
);
