using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.DTOs.Common;
using KeyhookBooking.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeyhookBooking.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class BookingsController(BookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Lists all bookings.
    /// </summary>
    [HttpGet("bookings")]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await bookingService.GetAllAsync());
    }

    /// <summary>
    /// Creates a booking for an available slot.
    /// </summary>
    /// <remarks>
    /// The requested date must match the slot availability. Only one active booking can exist for a slot/date pair.
    /// </remarks>
    [HttpPost("bookings")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest req)
    {
        if (string.IsNullOrEmpty(req.BookDate) || req.SlotId == 0 || req.TenantId == 0)
            return BadRequest(new { error = "date, slotId and tenantId required" });

        var (dto, status, error) = await bookingService.BookSlotAsync(req);

        return status switch {
            201 => Created($"/api/bookings/{dto!.Id}", dto),
            404 => NotFound(new { error }),
            409 => Conflict(new { error }),
            400 => BadRequest(new { error }),
            _ => StatusCode(status, new { error })
        };
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("book")]
    public Task<IActionResult> CreateLegacy([FromBody] CreateBookingRequest req) => Create(req);

    /// <summary>
    /// Cancels a booking as either the tenant or a manager.
    /// </summary>
    [HttpDelete("bookings/{id:int}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, [FromQuery] int cancelledBy)
    {
        if (cancelledBy == 0)
            return BadRequest(new { error = "cancelledBy (userId) query parameter required" });

        var (dto, status, error) = await bookingService.CancelBookingAsync(id, cancelledBy);

        return status switch {
            200 => Ok(dto),
            404 => NotFound(new { error }),
            409 => Conflict(new { error }),
            403 => StatusCode(403, new { error }),
            _ => StatusCode(status, new { error })
        };
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpDelete("book/{id:int}")]
    public Task<IActionResult> CancelLegacy(int id, [FromQuery] int cancelledBy) =>
        Cancel(id, cancelledBy);
}
