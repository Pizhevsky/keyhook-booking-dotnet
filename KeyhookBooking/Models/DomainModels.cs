namespace KeyhookBooking.Models;

public enum UserRole { Tenant, Manager }

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
}

public class Availability
{
    public int Id { get; set; }
    public int ManagerId { get; set; }
    public string SelectedDate { get; set; } = string.Empty;
    public string DaysOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public User? Manager { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public enum BookingStatus
{
    Active,
    CancelledByTenant,
    CancelledByManager
}

public class Booking
{
    public int Id { get; set; }
    public int SlotId { get; set; }
    public string BookDate { get; set; } = string.Empty;
    public int TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Active;
    public Availability? Slot { get; set; }
    public User? Tenant { get; set; }
}
