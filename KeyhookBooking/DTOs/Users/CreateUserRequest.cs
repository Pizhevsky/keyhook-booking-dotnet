namespace KeyhookBooking.DTOs.Users;

/// <summary>
/// Request body for creating a user.
/// </summary>
/// <param name="Name">User display name.</param>
/// <param name="Role">User role. Accepted values are tenant and manager.</param>
public sealed record CreateUserRequest(
    string Name,
    string Role
);
