using KeyhookBooking.Data;
using KeyhookBooking.Hubs;
using KeyhookBooking.Services;
using KeyhookBooking.Swagger;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Services

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(
        builder.Configuration.GetConnectionString("Default") ?? "Data Source=keyhook.db"
    )
);

builder.Services.AddSignalR();
builder.Services.AddScoped<IBroadcastService, BroadcastService>();

builder.Services.AddScoped<BookingValidationService>();
builder.Services.AddScoped<BookingWriter>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<UserService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc(
        "v1", 
        new OpenApiInfo
        {
            Title = "Keyhook Booking API",
            Version = "v1",
            Description = "Backend API for users, manager availability, tenant bookings, and SignalR booking events."
        }
    );

    c.SchemaFilter<ExampleSchemaFilter>();
    c.CustomOperationIds(api =>
        api.ActionDescriptor.RouteValues["controller"] + "_" +
        api.ActionDescriptor.RouteValues["action"]
    );

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .OfType<string>()
    .ToArray();

if (allowedOrigins.Length == 0) {
    allowedOrigins =
    [
        "http://localhost:1234",
        "http://localhost:3000",
        "http://localhost:5173"
    ];
}

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    )
);

// Build
var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Pipeline
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Keyhook Booking API v1");
        c.DocumentTitle = "Keyhook Booking API";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHub<BookingHub>("/bookingHub");

Console.WriteLine($"Server running on http://localhost:{ builder.Configuration["PORT"] ?? "4000" }");
await app.RunAsync($"http://0.0.0.0:{ builder.Configuration["PORT"] ?? "4000" }");
