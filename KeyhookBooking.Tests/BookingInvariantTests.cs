using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.Models;
using KeyhookBooking.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KeyhookBooking.Tests;

public class BookingInvariantTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        db.Users.AddRange(
            new User { Id = 1, Name = "Alice Tenant", Role = UserRole.Tenant },
            new User { Id = 2, Name = "Bob Tenant", Role = UserRole.Tenant },
            new User { Id = 3, Name = "Manager Mike", Role = UserRole.Manager }
        );

        db.Availabilities.AddRange(
            new Availability
            {
                Id = 10,
                ManagerId = 3,
                DaysOfWeek = "2;3",
                SelectedDate = string.Empty,
                StartTime = "10:00",
                EndTime = "11:00",
                TimeZone = "Pacific/Auckland"
            },
            new Availability
            {
                Id = 11,
                ManagerId = 3,
                DaysOfWeek = string.Empty,
                SelectedDate = "20/10/2099",
                StartTime = "13:00",
                EndTime = "14:00",
                TimeZone = "Pacific/Auckland"
            }
        );

        db.SaveChanges();

        return db;
    }

    private static BookingService CreateService(AppDbContext db)
    {
        var broadcast = new Mock<IBroadcastService>();
        broadcast.Setup(b => b.SendAsync(It.IsAny<string>(), It.IsAny<object>()))
                 .Returns(Task.CompletedTask);

        var validationService = new BookingValidationService(db);
        var bookingWriter = new BookingWriter(db, broadcast.Object);

        return new BookingService(db, broadcast.Object, validationService, bookingWriter);
    }

    private static CreateBookingRequest ValidRequest(
        int slotId = 10,
        string date = "21/10/2099",
        int tenantId = 1
    ) => new()
    {
        SlotId = slotId,
        BookDate = date,
        TenantId = tenantId,
        TenantTimeZone = "Pacific/Auckland"
    };

    [Fact]
    public void PastSlot_CheckNotInPast_ReturnsError()
    {
        var error = BookingTimeService.CheckNotInPast(
            bookDate: "01/01/2000",
            startTime: "10:00",
            slotTz: "Pacific/Auckland");

        Assert.NotNull(error);
        Assert.Contains("past", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FutureSlot_CheckNotInPast_ReturnsNull()
    {
        var error = BookingTimeService.CheckNotInPast(
            bookDate: "01/01/2099",
            startTime: "10:00",
            slotTz: "Pacific/Auckland");

        Assert.Null(error);
    }

    [Fact]
    public async Task RecurringSlotWrongWeekday_Booking_Returns400()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (_, status, error) = await svc.BookSlotAsync(ValidRequest(date: "24/10/2099"));

        Assert.Equal(400, status);
        Assert.Contains("not available", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OneOffSlotDifferentDate_Booking_Returns400()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (_, status, error) = await svc.BookSlotAsync(ValidRequest(slotId: 11, date: "21/10/2099"));

        Assert.Equal(400, status);
        Assert.Contains("not available", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OneOffSlotMatchingDate_Booking_Succeeds()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (dto, status, error) = await svc.BookSlotAsync(ValidRequest(slotId: 11, date: "20/10/2099"));

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task SameSlotSameDate_SecondActiveBooking_Returns409()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var req = ValidRequest();

        await svc.BookSlotAsync(req);
        var (_, status, error) = await svc.BookSlotAsync(req with { TenantId = 2 });

        Assert.Equal(409, status);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task SameSlotDifferentValidDate_TwoBookings_BothSucceed()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (dto1, status1, _) = await svc.BookSlotAsync(ValidRequest(date: "20/10/2099"));
        var (dto2, status2, _) = await svc.BookSlotAsync(ValidRequest(date: "21/10/2099", tenantId: 2));

        Assert.Equal(201, status1);
        Assert.Equal(201, status2);
        Assert.NotEqual(dto1!.Id, dto2!.Id);
    }

    [Fact]
    public async Task AfterCancellation_SlotCanBeRebooked()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (original, _, _) = await svc.BookSlotAsync(ValidRequest());

        Assert.NotNull(original);

        await svc.CancelBookingAsync(original.Id, requestingUserId: 1);

        var (rebooked, status, error) = await svc.BookSlotAsync(ValidRequest(tenantId: 2));

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(rebooked);
    }

    [Fact]
    public async Task Manager_AttemptingToBook_Returns404TenantNotFound()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (_, status, error) = await svc.BookSlotAsync(ValidRequest(tenantId: 3));

        Assert.Equal(404, status);
        Assert.Contains("tenant", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tenant_CancellingOwnBooking_Succeeds()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (booking, _, _) = await svc.BookSlotAsync(ValidRequest(tenantId: 1));
        var (dto, status, _) = await svc.CancelBookingAsync(booking!.Id, requestingUserId: 1);

        Assert.Equal(200, status);
        Assert.Equal("cancelled_by_tenant", dto!.Status);
        Assert.NotNull(dto.CancelledAt);
    }

    [Fact]
    public async Task Tenant_CancellingAnotherTenantBooking_Returns403()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (booking, _, _) = await svc.BookSlotAsync(ValidRequest(tenantId: 1));
        var (_, status, error) = await svc.CancelBookingAsync(booking!.Id, requestingUserId: 2);

        Assert.Equal(403, status);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Manager_CancellingAnyBooking_Succeeds()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (booking, _, _) = await svc.BookSlotAsync(ValidRequest(tenantId: 1));
        var (dto, status, _) = await svc.CancelBookingAsync(booking!.Id, requestingUserId: 3);

        Assert.Equal(200, status);
        Assert.Equal("cancelled_by_manager", dto!.Status);
    }

    [Fact]
    public async Task AlreadyCancelledBooking_CancelAgain_Returns409()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        var (booking, _, _) = await svc.BookSlotAsync(ValidRequest(tenantId: 1));
        await svc.CancelBookingAsync(booking!.Id, requestingUserId: 1);
        var (_, status, error) = await svc.CancelBookingAsync(booking.Id, requestingUserId: 1);

        Assert.Equal(409, status);
        Assert.Contains("already", error!, StringComparison.OrdinalIgnoreCase);
    }
}
