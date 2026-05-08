using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Availability;
using KeyhookBooking.Models;
using KeyhookBooking.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KeyhookBooking.Tests;

public class AvailabilityInvariantTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);

        db.Users.AddRange(
            new User { Id = 1, Name = "Alice Tenant", Role = UserRole.Tenant },
            new User { Id = 2, Name = "Manager Mike", Role = UserRole.Manager },
            new User { Id = 3, Name = "Manager Jane", Role = UserRole.Manager }
        );

        db.Availabilities.Add(new Availability
        {
            Id = 10,
            ManagerId = 2,
            SelectedDate = string.Empty,
            DaysOfWeek = "2;5",
            StartTime = "10:00",
            EndTime = "11:00",
            TimeZone = "Pacific/Auckland"
        });

        db.SaveChanges();

        return db;
    }

    private static AvailabilityService CreateService(AppDbContext db)
    {
        var broadcast = new Mock<IBroadcastService>();
        broadcast.Setup(b => b.SendAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        return new AvailabilityService(db, broadcast.Object);
    }

    [Fact]
    public async Task SlotWithActiveBookingOnAnyDate_Delete_Returns409AndKeepsSlot()
    {
        using var db = CreateDb();
        db.Bookings.Add(new Booking
        {
            Id = 100,
            SlotId = 10,
            TenantId = 1,
            BookDate = "24/10/2099",
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (_, status, error) = await service.DeleteAvailabilityAsync(slotId: 10, managerId: 2);

        Assert.Equal(409, status);
        Assert.Contains("active bookings", error!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delete", error!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.Availabilities.FindAsync(10));
    }

    [Fact]
    public async Task SlotWithOnlyCancelledBookings_Delete_SoftDeletesAndPreservesBookingHistory()
    {
        using var db = CreateDb();
        db.Bookings.Add(new Booking
        {
            Id = 101,
            SlotId = 10,
            TenantId = 1,
            BookDate = "24/10/2099",
            Status = BookingStatus.CancelledByTenant,
            CreatedAt = DateTime.UtcNow,
            CancelledAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (dto, status, error) = await service.DeleteAvailabilityAsync(slotId: 10, managerId: 2);

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(10, dto!.Id);

        var deletedSlot = await db.Availabilities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == 10);
        var visibleSlots = await service.GetAllAsync();
        var preservedBooking = await db.Bookings.FindAsync(101);

        Assert.NotNull(deletedSlot);
        Assert.True(deletedSlot!.IsDeleted);
        Assert.DoesNotContain(visibleSlots, a => a.Id == 10);
        Assert.NotNull(preservedBooking);
        Assert.Equal(10, preservedBooking!.SlotId);
    }

    [Fact]
    public async Task ManagerDeletingAnotherManagersSlot_Returns403AndKeepsSlot()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var (_, status, error) = await service.DeleteAvailabilityAsync(slotId: 10, managerId: 3);

        Assert.Equal(403, status);
        Assert.Contains("own availability", error!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await db.Availabilities.FindAsync(10));
    }

    [Fact]
    public async Task SlotWithActiveBooking_UpdateSchedule_Returns409AndKeepsOriginalSlot()
    {
        using var db = CreateDb();
        db.Bookings.Add(new Booking
        {
            Id = 102,
            SlotId = 10,
            TenantId = 1,
            BookDate = "20/10/2099",
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (_, status, error) = await service.UpdateAvailabilityAsync(
            10,
            new UpdateAvailabilityRequest(
                ManagerId: 2,
                SelectedDate: null,
                DaysOfWeek: "3",
                StartTime: "12:00",
                EndTime: "13:00",
                TimeZone: "Pacific/Auckland"
            )
        );

        Assert.Equal(409, status);
        Assert.Contains("active bookings", error!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("update", error!, StringComparison.OrdinalIgnoreCase);

        var slot = await db.Availabilities.FindAsync(10);

        Assert.Equal("2;5", slot!.DaysOfWeek);
        Assert.Equal("10:00", slot.StartTime);
    }

    [Fact]
    public async Task ManagerUpdatingAnotherManagersSlot_Returns403AndKeepsSlot()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var (_, status, error) = await service.UpdateAvailabilityAsync(
            10,
            new UpdateAvailabilityRequest(
                ManagerId: 3,
                SelectedDate: null,
                DaysOfWeek: "3",
                StartTime: "12:00",
                EndTime: "13:00",
                TimeZone: "Pacific/Auckland"
            )
        );

        Assert.Equal(403, status);
        Assert.Contains("own availability", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SameLocalTimeDifferentTimezone_CreateAvailability_Succeeds()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var (dto, status, error) = await service.CreateAvailabilityAsync(
            new CreateAvailabilityRequest(
                ManagerId: 2,
                SelectedDate: null,
                DaysOfWeek: "2;5",
                StartTime: "10:00",
                EndTime: "11:00",
                TimeZone: "America/New_York"
            )
        );

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(dto);
    }

    [Fact]
    public async Task SameLocalTimeSameTimezone_CreateAvailability_Returns409()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var (_, status, error) = await service.CreateAvailabilityAsync(
            new CreateAvailabilityRequest(
                ManagerId: 2,
                SelectedDate: null,
                DaysOfWeek: "2;5",
                StartTime: "10:00",
                EndTime: "11:00",
                TimeZone: "Pacific/Auckland"
            )
        );

        Assert.Equal(409, status);
        Assert.Contains("duplicate", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "", "10:00", "11:00", "Pacific/Auckland", "At least one")]
    [InlineData("20/10/2099", "2", "10:00", "11:00", "Pacific/Auckland", "Only one")]
    [InlineData("20/10/2099", "", "11:00", "10:00", "Pacific/Auckland", "later")]
    [InlineData("20/10/2099", "", "10:00", "11:00", "Not/ATimezone", "timezone")]
    [InlineData("20-10-2099", "", "10:00", "11:00", "Pacific/Auckland", "selectedDate")]
    [InlineData("", "1;8", "10:00", "11:00", "Pacific/Auckland", "daysOfWeek")]
    public void InvalidAvailabilityInput_ReturnsExpectedError(
        string selectedDate,
        string daysOfWeek,
        string start,
        string end,
        string timezone,
        string expected
    ){
        var error = AvailabilityService.ValidateAvailabilityInput(
            selectedDate,
            daysOfWeek,
            start,
            end,
            timezone
        );

        Assert.NotNull(error);
        Assert.Contains(expected, error!, StringComparison.OrdinalIgnoreCase);
    }
}
