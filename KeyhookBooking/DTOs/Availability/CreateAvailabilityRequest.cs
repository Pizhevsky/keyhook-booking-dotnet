namespace KeyhookBooking.DTOs.Availability;

/// <summary>
/// Request body for creating a manager availability slot.
/// </summary>
/// <param name="ManagerId">ID of the manager who owns the slot.</param>
/// <param name="SelectedDate">Optional single date in DD/MM/YYYY format. Use null or empty for recurring slots.</param>
/// <param name="DaysOfWeek">Optional recurring ISO weekdays, 1 for Monday through 7 for Sunday, separated by semicolons.</param>
/// <param name="StartTime">Slot start time in HH:mm format.</param>
/// <param name="EndTime">Slot end time in HH:mm format. Must be later than startTime.</param>
/// <param name="TimeZone">IANA timezone used when evaluating the slot time.</param>
public sealed record CreateAvailabilityRequest(
    int ManagerId,
    string? SelectedDate,
    string? DaysOfWeek,
    string StartTime,
    string EndTime,
    string TimeZone
);
