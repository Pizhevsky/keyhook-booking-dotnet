using KeyhookBooking.DTOs.Availability;
using KeyhookBooking.DTOs.Common;
using KeyhookBooking.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeyhookBooking.Controllers;

[ApiController]
[Route("api/availability")]
[Produces("application/json")]
public class AvailabilityController(AvailabilityService availabilityService) : ControllerBase
{
    /// <summary>
    /// Lists all availability slots.
    /// </summary>
    /// <returns>All manager availability slots.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await availabilityService.GetAllAsync());
    }

    /// <summary>
    /// Creates a manager availability slot.
    /// </summary>
    /// <remarks>
    /// Use either a single selected date or recurring ISO weekdays. Times are interpreted in the slot timezone.
    /// </remarks>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AvailabilityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAvailabilityRequest req)
    {
        var (dto, status, error) = await availabilityService.CreateAvailabilityAsync(req);

        return status switch {
            201 => Created($"/api/availability/{dto!.Id}", dto),
            404 => NotFound(new { error }),
            409 => Conflict(new { error }),
            403 => StatusCode(403, new { error }),
            400 => BadRequest(new { error }),
            _ => StatusCode(status, new { error })
        };
    }

    /// <summary>
    /// Updates an availability slot.
    /// </summary>
    /// <remarks>
    /// Active bookings prevent schedule-changing updates. Unspecified fields keep their current values.
    /// </remarks>
    [HttpPut("{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAvailabilityRequest req)
    {
        var (dto, status, error) = await availabilityService.UpdateAvailabilityAsync(id, req);

        return status switch {
            200 => Ok(dto),
            404 => NotFound(new { error }),
            409 => Conflict(new { error }),
            403 => StatusCode(403, new { error }),
            400 => BadRequest(new { error }),
            _ => StatusCode(status, new { error })
        };
    }

    /// <summary>
    /// Deletes an availability slot.
    /// </summary>
    /// <remarks>
    /// The manager must own the slot, and the slot must not have active bookings.
    /// </remarks>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(AvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, [FromQuery] int managerId)
    {
        var (dto, status, error) = await availabilityService.DeleteAvailabilityAsync(id, managerId);

        return status switch {
            200 => Ok(dto),
            404 => NotFound(new { error }),
            409 => Conflict(new { error }),
            403 => StatusCode(403, new { error }),
            400 => BadRequest(new { error }),
            _ => StatusCode(status, new { error })
        };
    }
}
