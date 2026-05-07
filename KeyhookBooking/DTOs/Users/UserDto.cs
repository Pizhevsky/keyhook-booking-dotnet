namespace KeyhookBooking.DTOs.Users;

/// <summary>
/// User returned by the API.
/// </summary>
/// <param name="Id">User ID.</param>
/// <param name="Name">User display name.</param>
/// <param name="Role">User role: tenant or manager.</param>
public sealed record UserDto(
    int Id,
    string Name,
    string Role
);
