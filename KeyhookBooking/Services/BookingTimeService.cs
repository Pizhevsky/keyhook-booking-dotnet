using NodaTime;

namespace KeyhookBooking.Services;

public static class BookingTimeService
{
    public static string? CheckSlotOccursOnDate(
        string selectedDate,
        string daysOfWeek,
        string bookDate
    ){
        if (!IsValidDate(bookDate))
            return "bookDate must be in DD/MM/YYYY format";

        if (!string.IsNullOrWhiteSpace(selectedDate)) {
            if (!IsValidDate(selectedDate))
                return "selectedDate must be in DD/MM/YYYY format";

            return selectedDate == bookDate
                ? null
                : "Slot is not available on this date";
        }

        var requestedDay = GetIsoDayOfWeek(bookDate);
        var availableDays = ParseDaysOfWeek(daysOfWeek).ToHashSet();

        return availableDays.Contains(requestedDay)
            ? null
            : "Slot is not available on this day";
    }

    public static string? CheckNotInPast(
        string bookDate,
        string startTime,
        string slotTz
    ){
        if (!IsValidDate(bookDate) || !IsHHmm(startTime))
            return null;

        if (string.IsNullOrWhiteSpace(slotTz))
            return null;

        var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull(slotTz);
        
        if (tz is null)
            return null;

        var parts = bookDate.Split('/');
        var localDate = new LocalDate(
            int.Parse(parts[2]), int.Parse(parts[1]), int.Parse(parts[0]));

        var timeParts = startTime.Split(':');
        var localTime = new LocalTime(int.Parse(timeParts[0]), int.Parse(timeParts[1]));
        var slotInstant = tz.AtLeniently(localDate + localTime).ToInstant();

        if (slotInstant < SystemClock.Instance.GetCurrentInstant())
            return "Slot is in the past";

        return null;
    }

    public static bool IsValidDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('/');

        return parts.Length == 3
            && int.TryParse(parts[0], out var day)
            && int.TryParse(parts[1], out var month)
            && int.TryParse(parts[2], out var year)
            && year is >= 1 and <= 9999
            && month is >= 1 and <= 12
            && day >= 1
            && day <= DateTime.DaysInMonth(year, month);
    }

    public static bool IsHHmm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], out var hour)
            && int.TryParse(parts[1], out var minute)
            && hour is >= 0 and <= 23
            && minute is >= 0 and <= 59
            && value.Length == 5;
    }

    public static bool IsValidTimezone(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && DateTimeZoneProviders.Tzdb.GetZoneOrNull(value) is not null;
    }

    public static bool IsStartBeforeEnd(string startTime, string endTime)
    {
        if (!IsHHmm(startTime) || !IsHHmm(endTime)) return false;

        return ToMinutes(startTime) < ToMinutes(endTime);
    }

    public static IReadOnlyList<int> ParseDaysOfWeek(string? daysOfWeek)
    {
        if (string.IsNullOrWhiteSpace(daysOfWeek))
            return Array.Empty<int>();

        return daysOfWeek
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => int.TryParse(v, out var day) ? day : -1)
            .Where(day => day is >= 1 and <= 7)
            .Distinct()
            .ToArray();
    }

    private static int GetIsoDayOfWeek(string date)
    {
        var parts = date.Split('/');
        var dt = new DateOnly(int.Parse(parts[2]), int.Parse(parts[1]), int.Parse(parts[0]));
        
        return dt.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dt.DayOfWeek;
    }

    private static int ToMinutes(string value)
    {
        var parts = value.Split(':');

        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }
}
