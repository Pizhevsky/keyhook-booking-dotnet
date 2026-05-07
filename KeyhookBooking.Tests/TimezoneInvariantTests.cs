using KeyhookBooking.Services;
using Xunit;

namespace KeyhookBooking.Tests;

public class TimezoneInvariantTests
{
    [Theory]
    [InlineData("Pacific/Auckland")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    public void PastDate_AnySlotTimezone_IsRejected(string timezone)
    {
        var error = BookingTimeService.CheckNotInPast(
            "01/01/2000",
            "10:00",
            timezone
        );

        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("Pacific/Auckland")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Tokyo")]
    public void FutureDate_AnySlotTimezone_IsAllowed(string timezone)
    {
        var error = BookingTimeService.CheckNotInPast(
            "01/01/2099",
            "10:00",
            timezone
        );

        Assert.Null(error);
    }

    [Fact]
    public void PastDate_ValidSlotTimezone_IsRejected()
    {
        var error = BookingTimeService.CheckNotInPast(
            "01/01/2000",
            "10:00",
            "Pacific/Auckland"
        );

        Assert.NotNull(error);
    }

    [Fact]
    public void InvalidSlotTimezone_IsAllowed_GracefulDegradation()
    {
        var error = BookingTimeService.CheckNotInPast(
            "01/01/2000",
            "10:00",
            "Not/ATimezone"
        );

        Assert.Null(error);
    }

    [Fact]
    public void MalformedDate_IsAllowed_GracefulDegradation()
    {
        var error = BookingTimeService.CheckNotInPast(
            "not-a-date",
            "10:00",
            "Pacific/Auckland"
        );
        
        Assert.Null(error);
    }
}
