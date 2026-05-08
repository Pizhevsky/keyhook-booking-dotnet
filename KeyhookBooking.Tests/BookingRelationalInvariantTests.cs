using KeyhookBooking.Data;
using KeyhookBooking.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeyhookBooking.Tests;

public class BookingRelationalInvariantTests
{
    [Fact]
    public async Task FilteredUniqueIndex_EnforcesActiveBookingUniqueness()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Bookings.Add(new Booking
        {
            SlotId = 1,
            TenantId = 1,
            BookDate = "01/01/2099",
            Status = BookingStatus.CancelledByTenant,
            CreatedAt = DateTime.UtcNow,
            CancelledAt = DateTime.UtcNow
        });

        db.Bookings.Add(new Booking
        {
            SlotId = 1,
            TenantId = 2,
            BookDate = "01/01/2099",
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        db.Bookings.Add(new Booking
        {
            SlotId = 1,
            TenantId = 1,
            BookDate = "01/01/2099",
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
