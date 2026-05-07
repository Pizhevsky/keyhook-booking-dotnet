using KeyhookBooking.DTOs.Common;
using KeyhookBooking.DTOs.Users;
using KeyhookBooking.Services;
using Microsoft.AspNetCore.Mvc;

namespace KeyhookBooking.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController(UserService userService) : ControllerBase
{
    /// <summary>
    /// Lists all users.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await userService.GetAllAsync());
    }

    /// <summary>
    /// Creates a tenant or manager user.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var (dto, status, error) = await userService.CreateAsync(req);

        return status switch {
            201 => CreatedAtAction(nameof(GetAll), routeValues: null, value: dto),
            400 => BadRequest(new { error }),
            _ => StatusCode(status, new { error })
        };
    }
}
