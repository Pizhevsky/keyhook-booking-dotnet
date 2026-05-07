using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyhookBooking.Services;

public class BookingService(
    AppDbContext db,
    IBroadcastService broadcast,
    BookingValidationService validationService,
    BookingWriter bookingWriter
){
    public async Task<IReadOnlyList<BookingDto>> GetAllAsync()
    {
        var rows = await db.Bookings.ToListAsync();

        return rows.Select(ToDto).ToArray();
    }

    public async Task<(BookingDto? booking, int errorStatus, string? error)> BookSlotAsync(
        CreateBookingRequest req
    ){
        var (status, error) = await validationService.ValidateForBookingAsync(req);
        if (error is not null)
            return (null, status, error);

        return await bookingWriter.CreateBookingAsync(req);
    }

    public async Task<(BookingDto? booking, int errorStatus, string? error)> CancelBookingAsync(
        int bookingId,
        int requestingUserId
    ){
        if (bookingId <= 0)
            return (null, 400, "bookingId must be a positive integer");

        if (requestingUserId <= 0)
            return (null, 400, "cancelledBy must be a positive integer");

        var booking = await db.Bookings.FindAsync(bookingId);
        if (booking is null)
            return (null, 404, "Booking not found");

        if (booking.Status != BookingStatus.Active)
            return (null, 409, $"Booking is already {SerialiseStatus(booking.Status)}");

        var requestingUser = await db.Users.FindAsync(requestingUserId);
        if (requestingUser is null)
            return (null, 404, "Requesting user not found");

        if (requestingUser.Role == UserRole.Tenant && booking.TenantId != requestingUserId)
            return (null, 403, "Tenants can only cancel their own bookings");

        booking.Status = requestingUser.Role == UserRole.Manager
            ? BookingStatus.CancelledByManager
            : BookingStatus.CancelledByTenant;
        booking.CancelledAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var dto = ToDto(booking);
        await broadcast.SendAsync("BOOKING_CANCELLED", dto);

        return (dto, 200, null);
    }

    internal static string SerialiseStatus(BookingStatus status) => 
        status switch {
            BookingStatus.Active => "active",
            BookingStatus.CancelledByTenant => "cancelled_by_tenant",
            BookingStatus.CancelledByManager => "cancelled_by_manager",
            _ => status.ToString().ToLowerInvariant()
        };

    internal static BookingDto ToDto(Booking b) =>
        new(
            b.Id,
            b.SlotId,
            b.TenantId,
            b.BookDate,
            b.CreatedAt,
            SerialiseStatus(b.Status),
            b.CancelledAt
        );
}
