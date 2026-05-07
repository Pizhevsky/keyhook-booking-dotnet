namespace KeyhookBooking.DTOs.Availability;

/// <summary>
/// Availability slot exposed by the API.
/// </summary>
/// <param name="Id">Availability slot ID.</param>
/// <param name="ManagerId">ID of the manager who owns the slot.</param>
/// <param name="SelectedDate">Single available date in DD/MM/YYYY format. Empty for recurring slots.</param>
/// <param name="DaysOfWeek">Recurring ISO weekdays, 1 for Monday through 7 for Sunday, separated by semicolons.</param>
/// <param name="StartTime">Slot start time in HH:mm format.</param>
/// <param name="EndTime">Slot end time in HH:mm format.</param>
/// <param name="TimeZone">IANA timezone used when evaluating the slot time.</param>
public sealed record AvailabilityDto(
    int Id,
    int ManagerId,
    string SelectedDate,
    string DaysOfWeek,
    string StartTime,
    string EndTime,
    string TimeZone
);
