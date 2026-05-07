# Keyhook Booking .NET API

This repository contains a C# / ASP.NET Core port of the Keyhook Booking backend, built as an alternative API for the existing React client.

The original backend was built with Node.js, Express, Sequelize, and plain WebSockets. This version keeps the same booking domain and REST contract, while rebuilding the backend with ASP.NET Core, Entity Framework Core, SQLite, NodaTime, and SignalR.

The goal was not only to reproduce the original API, but to apply .NET idioms properly and strengthen the core booking invariants around availability, timezone handling, conflict prevention, cancellation, and schedule changes.

## Architecture notes

The controller layer is kept thin. Controllers adapt HTTP requests into service calls, while booking and availability rules live in services.

The database is not treated as a passive storage layer. The app performs validation before writing, but the filtered unique index also protects the most important invariant: only one active booking can exist for the same slot and date.

Dates are kept in the frontend-compatible `DD/MM/YYYY` format at the API boundary. Internally, timezone-sensitive checks are handled with NodaTime.

## What this demonstrates

- Porting an existing Node.js backend contract to ASP.NET Core
- Preserving frontend compatibility while changing backend technology
- Applying Entity Framework Core for persistence, migrations, and relational constraints
- Protecting booking invariants on the server, not only in the UI
- Handling timezone-sensitive availability checks with NodaTime
- Using SignalR for real-time booking and availability events
- Testing domain rules around booking conflicts, cancellation, ownership, and schedule changes

## Why this exists

The original backend was built with Node.js, Express, Sequelize, SQLite, and plain WebSockets. This version rebuilds the backend with ASP.NET Core, Entity Framework Core, SQLite, NodaTime, and SignalR.

Both backends expose the same core REST contract and the same real-time message shape. The React client can work with either backend through a small transport switch: plain WebSocket for the Node.js server, SignalR for the .NET server.

The goal was not only to reproduce the existing API, but to apply .NET idioms properly and strengthen the backend rules around availability, timezone handling, ownership checks, cancellation, and conflict prevention.

The latest Node.js backend already protects important invariants such as slot/date occurrence checks, soft cancellation, active-booking conflict checks, and delete protection for availability with active bookings. The .NET version keeps those behaviours and extends the protection to schedule-changing availability updates as well.

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core on .NET 10 |
| Data access | Entity Framework Core with SQLite |
| Real-time | SignalR |
| Timezone | NodaTime (IANA tz database) |
| Tests | xUnit + EF InMemory + Moq |
| API docs | Swagger / OpenAPI |

## Booking invariants

These are the rules the server enforces. The frontend performs convenience validation; the server is the source of truth.

| # | Rule | Where enforced |
|---|---|---|
| 1 | Only tenants can create bookings | `BookingValidationService.ValidateForBookingAsync` |
| 2 | The slot must exist | `BookingValidationService.ValidateForBookingAsync` |
| 3 | `bookDate` must fall on a day the slot actually occurs | `BookingTimeService.CheckSlotOccursOnDate` |
| 4 | The slot must not be in the past (slot timezone) | `BookingTimeService.CheckNotInPast` |
| 5 | Only one active booking per slot/date pair | App-level check + filtered unique DB index |
| 6 | Tenants can only cancel their own bookings | `BookingService.CancelBookingAsync` |
| 7 | Availability with active bookings cannot be edited or deleted | `AvailabilityService.AssertNoActiveBookingsAsync` |

### On double-booking prevention

Rule 5 is enforced at two layers intentionally. The application-level check inside a transaction gives a clean 409 response. The filtered unique index on `(SlotId, BookDate) WHERE Status = 'Active'` is the hard guarantee — it catches concurrent requests that race past the application check before either transaction commits.

The index is filtered so cancelled bookings are excluded: a cancelled row does not block re-booking of the same slot and date, which is correct — the booking history is preserved while the slot becomes available again.

## Requirements

- .NET 10 SDK
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

## Getting started

```bash
cd KeyhookBooking
dotnet restore
dotnet ef migrations add Init
dotnet ef database update
dotnet run
```

The API listens on `http://localhost:4000` by default. Swagger is available in development at `http://localhost:4000/swagger`.

The `PORT` key in `appsettings.json` overrides the default port.

## Running tests

Tests use EF Core's InMemory provider, so no local database or migration is needed:

```bash
cd KeyhookBooking.Tests
dotnet test
```

To see individual test names and timing:

```bash
dotnet test --logger "console;verbosity=detailed"
```

To run a specific test class:

```bash
dotnet test --filter "ClassName=BookingInvariantTests"
```

Most tests use EF Core's InMemory provider because they focus on business rules. The filtered unique index is a relational database guarantee, so a production-grade test suite would also include a SQLite or PostgreSQL integration test for concurrent booking attempts.

## Real-time events (SignalR)

Connect to `/bookingHub`. All events arrive on the `"message"` channel with this shape:

```json
{ "type": "BOOKING_CREATED", "payload": { ... } }
```

| Event | Triggered when |
|---|---|
| `USER_CREATED` | A new user is created |
| `AVAILABILITY_CREATED` | A manager adds a slot |
| `AVAILABILITY_UPDATED` | A slot's schedule changes |
| `AVAILABILITY_DELETED` | A slot is removed |
| `BOOKING_CREATED` | A tenant books a slot |
| `BOOKING_CANCELLED` | A booking is soft-cancelled |

Bookings are never hard-deleted. Cancellation sets `status` to `cancelled_by_tenant` or `cancelled_by_manager` and records a `cancelledAt` timestamp. This preserves history and is what allows re-booking the same slot after a cancellation.

### Connecting from the React client

```ts
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('http://localhost:4000/bookingHub')
  .withAutomaticReconnect()
  .build();

connection.on('message', (data) => {
  // data.type, data.payload
});

await connection.start();
```

Set `SERVER_TYPE=.net` in the client `.env` to use SignalR instead of plain WebSocket.

## API reference

Full interactive documentation is available at `/swagger` in development. The sections below summarise the contract.

### Users

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/users` | List all users |
| `POST` | `/api/users` | Create a user |

```json
POST /api/users
{ "name": "Jane Manager", "role": "manager" }
```

`role` accepts `tenant` or `manager`.

### Availability

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/availability` | List all slots |
| `POST` | `/api/availability` | Create a slot |
| `PUT` | `/api/availability/{id}` | Update a slot |
| `DELETE` | `/api/availability/{id}?managerId={id}` | Delete a slot |

```json
POST /api/availability
{
  "managerId": 3,
  "daysOfWeek": "1;5",
  "selectedDate": null,
  "startTime": "10:00",
  "endTime": "12:00",
  "timeZone": "Pacific/Auckland"
}
```

`daysOfWeek` uses ISO weekday numbers (1 = Monday, 7 = Sunday) separated by semicolons. Use `selectedDate` in `DD/MM/YYYY` format for a one-off slot instead. At least one of the two is required. `timeZone` must be a valid IANA identifier.

Updates and deletes are rejected with 409 if the slot has any active bookings.

### Bookings

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/bookings` | List all bookings |
| `POST` | `/api/bookings` | Create a booking |
| `DELETE` | `/api/bookings/{id}?cancelledBy={userId}` | Cancel a booking |

```json
POST /api/bookings
{
  "slotId": 1,
  "bookDate": "14/07/2099",
  "tenantId": 1,
  "tenantTimeZone": "Pacific/Auckland"
}
```

`bookDate` must be in `DD/MM/YYYY` format and must fall on a day the slot occurs. Past slots are rejected using the slot's own timezone. `tenantTimeZone` is accepted for client compatibility but the slot timezone is used for the past-slot check.

## Project structure

```
KeyhookBooking/
  Controllers/        HTTP adapters — no business logic
  Data/               EF Core DbContext, schema config, seed data
  DTOs/               Request and response records
  Hubs/               SignalR hub
  Models/             Domain entities and status enums
  Services/           All business rules live here
    BookingService              Booking orchestration
    BookingValidationService    Request, tenant, slot, occurrence, and past-slot validation
    BookingWriter               Transactional booking creation
    BookingTimeService          Date, weekday, and timezone rules
    AvailabilityService         Availability CRUD with active-booking guards
    BroadcastService            SignalR wrapper

KeyhookBooking.Tests/
  BookingInvariantTests.cs      All booking rules including occurrence check
  AvailabilityInvariantTests.cs  Edit/delete guards, ownership, validation
  TimezoneInvariantTests.cs      Past-slot rejection across multiple timezones
```

## Seed data

The database is seeded with four demo users and three availability slots on first run:

| Id | Name | Role |
|---|---|---|
| 1 | Alice Tenant | tenant |
| 2 | Bob Tenant | tenant |
| 3 | Manager Mike | manager |
| 4 | Manager Jane | manager |

## Known trade-offs

Authentication is simulated — user identity comes from request parameters rather than a session or token. This is intentional for the demo. Production would add JWT or cookie auth and derive user identity from the token rather than accepting it from the client.

SQLite is used for local portability. PostgreSQL would be the production choice, and the filtered unique index syntax would change slightly (`WHERE "Status" = 'Active'` rather than `[Status] = 'Active'`).

CORS currently allows three hardcoded localhost origins. A production configuration would read allowed origins from environment variables.
