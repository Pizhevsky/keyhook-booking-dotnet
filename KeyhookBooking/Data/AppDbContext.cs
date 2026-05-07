using Microsoft.EntityFrameworkCore;
using KeyhookBooking.Models;

namespace KeyhookBooking.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().IsRequired();
            e.HasMany(u => u.Availabilities)
                .WithOne(a => a.Manager)
                .HasForeignKey(a => a.ManagerId);
        });

        modelBuilder.Entity<Availability>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.SelectedDate).IsRequired();
            e.Property(a => a.DaysOfWeek).IsRequired();
            e.Property(a => a.StartTime).IsRequired();
            e.Property(a => a.EndTime).IsRequired();
            e.Property(a => a.TimeZone).IsRequired();
            e.HasMany(a => a.Bookings)
                .WithOne(b => b.Slot)
                .HasForeignKey(b => b.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BookDate).IsRequired();
            e.Property(b => b.Status).HasConversion<string>().IsRequired();
            e.Property(b => b.CancelledAt).IsRequired(false);
            e.HasIndex(b => new { b.SlotId, b.BookDate })
                .IsUnique()
                .HasFilter("[Status] = 'Active'");
        });

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Name = "Alice Tenant", Role = UserRole.Tenant },
            new User { Id = 2, Name = "Bob Tenant", Role = UserRole.Tenant },
            new User { Id = 3, Name = "Manager Mike", Role = UserRole.Manager },
            new User { Id = 4, Name = "Manager Jane", Role = UserRole.Manager }
        );

        modelBuilder.Entity<Availability>().HasData(
            new Availability { Id = 1, ManagerId = 3, DaysOfWeek = "1;5", SelectedDate = "", StartTime = "10:00", EndTime = "12:00", TimeZone = "Pacific/Auckland" },
            new Availability { Id = 2, ManagerId = 4, DaysOfWeek = "3", SelectedDate = "", StartTime = "14:00", EndTime = "16:00", TimeZone = "Pacific/Auckland" },
            new Availability { Id = 3, ManagerId = 3, DaysOfWeek = "5", SelectedDate = "", StartTime = "13:00", EndTime = "14:00", TimeZone = "Pacific/Auckland" }
        );
    }
}
