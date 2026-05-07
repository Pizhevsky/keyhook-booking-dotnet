using KeyhookBooking.Data;
using KeyhookBooking.DTOs.Users;
using KeyhookBooking.Models;
using Microsoft.EntityFrameworkCore;

namespace KeyhookBooking.Services;

public class UserService(AppDbContext db, IBroadcastService broadcast)
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync()
    {
        return await db.Users
            .Select(u => new UserDto(u.Id, u.Name, u.Role.ToString().ToLower()))
            .ToListAsync();
    }

    public async Task<(UserDto? user, int status, string? error)> CreateAsync(CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return (null, 400, "name is required and must be a non-empty string");

        if (req.Name.Length > 200)
            return (null, 400, "name must be 200 characters or fewer");

        if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: true, out var role) ||
            !Enum.IsDefined(role))
            return (null, 400, "role must be tenant or manager");

        var user = new User { 
            Name = req.Name.Trim(), 
            Role = role 
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = new UserDto(user.Id, user.Name, user.Role.ToString().ToLower());
        await broadcast.SendAsync("USER_CREATED", dto);

        return (dto, 201, null);
    }
}
