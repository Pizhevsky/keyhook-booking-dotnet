using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyhookBooking.Services;

public class BookingValidationService(AppDbContext db)
{
    public async Task<(int status, string? error)> ValidateForBookingAsync(
        CreateBookingRequest req
    ){
        var requestError = ValidateBookingRequest(req);
        if (requestError is not null)
            return (400, requestError);

        if (!await TenantExistsAsync(req.TenantId))
            return (404, "Tenant not found");

        var slot = await FindSlotAsync(req.SlotId);
        if (slot is null)
            return (404, "Slot not found");

        var slotError = ValidateSlotForBooking(slot, req);
        if (slotError is not null)
            return (400, slotError);

        return (200, null);
    }

    private async Task<bool> TenantExistsAsync(int tenantId)
    {
        return await db.Users.AnyAsync(u => u.Id == tenantId && u.Role == UserRole.Tenant);
    }

    private async Task<Availability?> FindSlotAsync(int slotId)
    {
        return await db.Availabilities.FindAsync(slotId);
    }

    private static string? ValidateBookingRequest(CreateBookingRequest req)
    {
        if (req.SlotId <= 0)
            return "slotId must be a positive integer";

        if (req.TenantId <= 0)
            return "tenantId must be a positive integer";
            
        if (!BookingTimeService.IsValidDate(req.BookDate))
            return "bookDate must be in DD/MM/YYYY format";

        return null;
    }

    private static string? ValidateSlotForBooking(Availability slot, CreateBookingRequest req)
    {
        return BookingTimeService.CheckSlotOccursOnDate(slot.SelectedDate, slot.DaysOfWeek, req.BookDate)
            ?? BookingTimeService.CheckNotInPast(req.BookDate, slot.StartTime, slot.TimeZone);
    }
}
