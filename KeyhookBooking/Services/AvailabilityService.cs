using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Availability;
using KeyhookBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyhookBooking.Services;

public class AvailabilityService(AppDbContext db, IBroadcastService broadcast)
{
    public async Task<IReadOnlyList<AvailabilityDto>> GetAllAsync()
    {
        var rows = await db.Availabilities
            .Where(a => !a.IsDeleted)
            .ToListAsync();

        return rows.Select(ToDto).ToArray();
    }

    public async Task<(AvailabilityDto? availability, int status, string? error)> CreateAvailabilityAsync(
        CreateAvailabilityRequest req
    ){
        var validation = ValidateAvailabilityInput(
            req.SelectedDate,
            req.DaysOfWeek,
            req.StartTime,
            req.EndTime,
            req.TimeZone
        );

        if (validation is not null)
            return (null, 400, validation);

        var manager = await db.Users.FirstOrDefaultAsync(u => 
            u.Id == req.ManagerId && 
            u.Role == UserRole.Manager
        );
        if (manager is null)
            return (null, 404, "Manager not found");

        var selectedDate = req.SelectedDate ?? string.Empty;
        var daysOfWeek = req.DaysOfWeek ?? string.Empty;

        var duplicate = await db.Availabilities.FirstOrDefaultAsync(a =>
            !a.IsDeleted &&
            a.ManagerId == req.ManagerId &&
            a.SelectedDate == selectedDate &&
            a.DaysOfWeek == daysOfWeek &&
            a.StartTime == req.StartTime &&
            a.EndTime == req.EndTime &&
            a.TimeZone == req.TimeZone
        );

        if (duplicate is not null)
            return (null, 409, "Duplicate slot");

        var slot = new Availability
        {
            ManagerId = req.ManagerId,
            SelectedDate = selectedDate,
            DaysOfWeek = daysOfWeek,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            TimeZone = req.TimeZone
        };

        db.Availabilities.Add(slot);
        await db.SaveChangesAsync();

        var dto = ToDto(slot);
        await broadcast.SendAsync("AVAILABILITY_CREATED", dto);

        return (dto, 201, null);
    }

    public async Task<(AvailabilityDto? availability, int status, string? error)> UpdateAvailabilityAsync(
        int slotId,
        UpdateAvailabilityRequest req
    ){
        if (slotId <= 0)
            return (null, 400, "slotId must be a positive integer");

        if (req.ManagerId is null || req.ManagerId <= 0)
            return (null, 400, "managerId required");

        var slot = await db.Availabilities.FirstOrDefaultAsync(a => a.Id == slotId && !a.IsDeleted);
        if (slot is null)
            return (null, 404, "Slot not found");

        var ownershipError = await AssertManagerOwnsSlotAsync(slot, req.ManagerId.Value);
        if (ownershipError is not null)
            return (null, 403, ownershipError);

        var nextSelectedDate = req.SelectedDate ?? slot.SelectedDate;
        var nextDaysOfWeek = req.DaysOfWeek ?? slot.DaysOfWeek;
        var nextStartTime = string.IsNullOrWhiteSpace(req.StartTime) ? slot.StartTime : req.StartTime;
        var nextEndTime = string.IsNullOrWhiteSpace(req.EndTime) ? slot.EndTime : req.EndTime;
        var nextTimeZone = string.IsNullOrWhiteSpace(req.TimeZone) ? slot.TimeZone : req.TimeZone;

        var validation = ValidateAvailabilityInput(
            nextSelectedDate,
            nextDaysOfWeek,
            nextStartTime,
            nextEndTime,
            nextTimeZone
        );
        if (validation is not null)
            return (null, 400, validation);

        var isScheduleChanged =
            slot.SelectedDate != nextSelectedDate ||
            slot.DaysOfWeek != nextDaysOfWeek ||
            slot.StartTime != nextStartTime ||
            slot.EndTime != nextEndTime ||
            slot.TimeZone != nextTimeZone;

        var activeBookingError = await ValidateScheduleCanChangeAsync(
            slotId,
            isScheduleChanged
        );
        if (activeBookingError is not null)
            return (null, 409, activeBookingError);

        var duplicate = await db.Availabilities.FirstOrDefaultAsync(a =>
            !a.IsDeleted &&
            a.Id != slotId &&
            a.ManagerId == slot.ManagerId &&
            a.SelectedDate == nextSelectedDate &&
            a.DaysOfWeek == nextDaysOfWeek &&
            a.StartTime == nextStartTime &&
            a.EndTime == nextEndTime &&
            a.TimeZone == nextTimeZone
        );
        if (duplicate is not null)
            return (null, 409, "Duplicate slot");

        slot.SelectedDate = nextSelectedDate;
        slot.DaysOfWeek = nextDaysOfWeek;
        slot.StartTime = nextStartTime;
        slot.EndTime = nextEndTime;
        slot.TimeZone = nextTimeZone;

        await db.SaveChangesAsync();

        var dto = ToDto(slot);
        await broadcast.SendAsync("AVAILABILITY_UPDATED", dto);

        return (dto, 200, null);
    }

    public async Task<(AvailabilityDto? availability, int status, string? error)> DeleteAvailabilityAsync(
        int slotId,
        int managerId
    ){
        var requestError = ValidateDeleteAvailabilityRequest(slotId, managerId);
        if (requestError is not null)
            return (null, 400, requestError);

        var slot = await db.Availabilities.FirstOrDefaultAsync(a => a.Id == slotId && !a.IsDeleted);
        if (slot is null)
            return (null, 404, "Slot not found");

        var ownershipError = await AssertManagerOwnsSlotAsync(slot, managerId);
        if (ownershipError is not null)
            return (null, 403, ownershipError);

        var activeBookingError = await AssertNoActiveBookingsAsync(slotId, "delete");
        if (activeBookingError is not null)
            return (null, 409, activeBookingError);

        return await SoftDeleteAvailabilityInTransactionAsync(slot);
    }

    private async Task<(AvailabilityDto? availability, int status, string? error)> SoftDeleteAvailabilityInTransactionAsync(
        Availability slot
    ){
        var dto = ToDto(slot);

        var useTransaction = db.Database.IsRelational();
        await using var tx = useTransaction
            ? await db.Database.BeginTransactionAsync()
            : null;

        try {
            slot.IsDeleted = true;
            await db.SaveChangesAsync();

            if (tx is not null) 
                await tx.CommitAsync();

            await broadcast.SendAsync("AVAILABILITY_DELETED", dto);

            return (dto, 200, null);
        } catch {
            if (tx is not null) 
                await tx.RollbackAsync();

            throw;
        }
    }

    private static string? ValidateDeleteAvailabilityRequest(int slotId, int managerId)
    {
        if (slotId <= 0)
            return "slotId must be a positive integer";

        if (managerId <= 0)
            return "managerId query parameter required";

        return null;
    }

    private async Task<string?> ValidateScheduleCanChangeAsync(
        int slotId,
        bool isScheduleChanged
    ){
        if (!isScheduleChanged)
            return null;

        var activeBookingError = await AssertNoActiveBookingsAsync(slotId, "update");

        return activeBookingError;
    }

    internal async Task<string?> AssertManagerOwnsSlotAsync(Availability slot, int managerId)
    {
        var manager = await db.Users.FirstOrDefaultAsync(
            u => u.Id == managerId && u.Role == UserRole.Manager);

        if (manager is null)
            return "Only managers can change availability";

        return slot.ManagerId == managerId
            ? null
            : "Managers can only change their own availability";
    }

    internal async Task<string?> AssertNoActiveBookingsAsync(int slotId, string operation = "delete")
    {
        var hasActiveBooking = await db.Bookings.AnyAsync(b =>
            b.SlotId == slotId && b.Status == BookingStatus.Active);

        return hasActiveBooking
            ? $"Cannot {operation} availability with active bookings"
            : null;
    }

    internal static string? ValidateAvailabilityInput(
        string? selectedDate,
        string? daysOfWeek,
        string? startTime,
        string? endTime,
        string? timeZone
    ){
        var hasSelectedDate = !string.IsNullOrWhiteSpace(selectedDate);
        var hasDaysOfWeek = !string.IsNullOrWhiteSpace(daysOfWeek);

        if (!hasSelectedDate && !hasDaysOfWeek)
            return "At least one of daysOfWeek or selectedDate is required";

        if (hasSelectedDate && hasDaysOfWeek)
            return "Only one of daysOfWeek or selectedDate is allowed";

        if (hasSelectedDate && !BookingTimeService.IsValidDate(selectedDate))
            return "selectedDate must be in DD/MM/YYYY format";

        if (hasDaysOfWeek) {
            var originalParts = daysOfWeek!.Split(
                ';', 
                StringSplitOptions.RemoveEmptyEntries | 
                StringSplitOptions.TrimEntries
            );
            var parsed = BookingTimeService.ParseDaysOfWeek(daysOfWeek);

            if (originalParts.Length == 0 || parsed.Count != originalParts.Length)
                return "daysOfWeek must contain day numbers 1-7 separated by ;";
        }

        if (!BookingTimeService.IsHHmm(startTime))
            return "startTime must be in HH:mm format";

        if (!BookingTimeService.IsHHmm(endTime))
            return "endTime must be in HH:mm format";

        if (!BookingTimeService.IsStartBeforeEnd(startTime!, endTime!))
            return "endTime must be later than startTime on the same day";

        if (!BookingTimeService.IsValidTimezone(timeZone))
            return "timeZone must be a valid IANA timezone";

        return null;
    }

    internal static AvailabilityDto ToDto(Availability a) =>
        new(a.Id, a.ManagerId, a.SelectedDate, a.DaysOfWeek, a.StartTime, a.EndTime, a.TimeZone);
}
