using KeyhookBooking.DTOs.Availability;
using KeyhookBooking.DTOs.Bookings;
using KeyhookBooking.DTOs.Common;
using KeyhookBooking.DTOs.Users;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace KeyhookBooking.Swagger;

public sealed class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        ApplyPropertyDescriptions(schema, context.Type);

        schema.Example = context.Type.Name switch {
            nameof(CreateUserRequest) => Obj(
                ("name", "Jane Manager"),
                ("role", "manager")
            ),
            nameof(UserDto) => Obj(
                ("id", 3),
                ("name", "Manager Mike"),
                ("role", "manager")
            ),
            nameof(CreateAvailabilityRequest) => Obj(
                ("managerId", 3),
                ("selectedDate", null),
                ("daysOfWeek", "1;5"),
                ("startTime", "10:00"),
                ("endTime", "12:00"),
                ("timeZone", "Pacific/Auckland")
            ),
            nameof(UpdateAvailabilityRequest) => Obj(
                ("managerId", 3),
                ("selectedDate", "01/01/2099"),
                ("daysOfWeek", null),
                ("startTime", "13:00"),
                ("endTime", "14:00"),
                ("timeZone", "Pacific/Auckland")
            ),
            nameof(AvailabilityDto) => Obj(
                ("id", 1),
                ("managerId", 3),
                ("selectedDate", ""),
                ("daysOfWeek", "1;5"),
                ("startTime", "10:00"),
                ("endTime", "12:00"),
                ("timeZone", "Pacific/Auckland")
            ),
            nameof(CreateBookingRequest) => Obj(
                ("slotId", 1),
                ("bookDate", "01/01/2099"),
                ("tenantId", 1),
                ("tenantTimeZone", "Pacific/Auckland")
            ),
            nameof(BookingDto) => Obj(
                ("id", 1),
                ("slotId", 1),
                ("tenantId", 1),
                ("bookDate", "01/01/2099"),
                ("createdAt", "2026-05-06T10:00:00Z"),
                ("status", "active"),
                ("cancelledAt", null)
            ),
            nameof(ErrorResponse) => Obj(("error", "Slot not found")),
            _ => schema.Example
        };
    }

    private static void ApplyPropertyDescriptions(OpenApiSchema schema, Type type)
    {
        var descriptions = type.Name switch {
            nameof(CreateUserRequest) => new Dictionary<string, string>
            {
                ["name"] = "User display name.",
                ["role"] = "User role. Accepted values are tenant and manager."
            },
            nameof(CreateAvailabilityRequest) => AvailabilityRequestDescriptions(),
            nameof(UpdateAvailabilityRequest) => AvailabilityRequestDescriptions(),
            nameof(CreateBookingRequest) => new Dictionary<string, string>
            {
                ["slotId"] = "ID of the availability slot to book.",
                ["bookDate"] = "Requested booking date in DD/MM/YYYY format.",
                ["tenantId"] = "ID of the tenant making the booking.",
                ["tenantTimeZone"] = "Optional tenant timezone. The slot timezone is used for past-slot validation."
            },
            _ => null
        };

        if (descriptions is null)
            return;

        foreach (var (propertyName, description) in descriptions) {
            if (schema.Properties.TryGetValue(propertyName, out var property))
                property.Description = description;
        }
    }

    private static Dictionary<string, string> AvailabilityRequestDescriptions() =>
        new() {
            ["managerId"] = "ID of the manager who owns the slot.",
            ["selectedDate"] = "Optional single date in DD/MM/YYYY format. Use null or empty for recurring slots.",
            ["daysOfWeek"] = "Optional recurring ISO weekdays, 1 for Monday through 7 for Sunday, separated by semicolons.",
            ["startTime"] = "Slot start time in HH:mm format.",
            ["endTime"] = "Slot end time in HH:mm format. Must be later than startTime.",
            ["timeZone"] = "IANA timezone used when evaluating the slot time."
        };

    private static OpenApiObject Obj(params (string Key, object? Value)[] values)
    {
        var obj = new OpenApiObject();

        foreach (var (key, value) in values)
            obj[key] = Any(value);

        return obj;
    }

    private static IOpenApiAny Any(object? value) =>
        value switch {
            null => new OpenApiNull(),
            int v => new OpenApiInteger(v),
            string v => new OpenApiString(v),
            bool v => new OpenApiBoolean(v),
            _ => new OpenApiString(value.ToString())
        };
}
