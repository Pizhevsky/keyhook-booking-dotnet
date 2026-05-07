using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyhookBooking.Services;

public class BookingWriter(AppDbContext db, IBroadcastService broadcast)
{
    private const string SlotAlreadyBookedError = "Slot is already booked for this date";

    public async Task<(BookingDto? booking, int errorStatus, string? error)> CreateBookingAsync(
        CreateBookingRequest req
    ){
        var booking = NewActiveBooking(req);

        var useTransaction = db.Database.IsRelational();
        await using var tx = useTransaction
            ? await db.Database.BeginTransactionAsync()
            : null;

        try {
            if (await ActiveBookingExistsAsync(req.SlotId, req.BookDate)) {
                if (tx is not null) 
                    await tx.RollbackAsync();

                return (null, 409, SlotAlreadyBookedError);
            }

            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            if (tx is not null) 
                await tx.CommitAsync();

            var dto = BookingService.ToDto(booking);
            await broadcast.SendAsync("BOOKING_CREATED", dto);
            
            return (dto, 201, null);
        } catch (DbUpdateException) {
            if (tx is not null) 
                await tx.RollbackAsync();

            return (null, 409, SlotAlreadyBookedError);
        } catch {
            if (tx is not null) 
                await tx.RollbackAsync();
                
            throw;
        }
    }

    private async Task<bool> ActiveBookingExistsAsync(int slotId, string bookDate)
    {
        return await db.Bookings.AnyAsync(b => 
            b.SlotId == slotId &&
            b.BookDate == bookDate &&
            b.Status == BookingStatus.Active
        );
    }

    private static Booking NewActiveBooking(CreateBookingRequest req)
    {
        return new Booking
        {
            SlotId = req.SlotId,
            BookDate = req.BookDate,
            TenantId = req.TenantId,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }
}
