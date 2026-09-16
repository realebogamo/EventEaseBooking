# EventEase Booking

Venue booking system for the IIE CLDV7111 Cloud Development Portfolio of Evidence.
Phase 1 is an **internal admin tool** for booking specialists — customers do not
book for themselves; a specialist enters bookings taken by phone, email, or in
person, and can load an event before a venue has been assigned to it.

Built with ASP.NET Core MVC + Entity Framework Core, deployed to Azure App
Service + Azure SQL Database (+ Blob Storage from Part 2).

## Project status: Part 1 — Foundation

- [x] Venue / Event / Booking CRUD (Controllers, Views, EF Core)
- [x] Local SQL schema + seed data script (`Database/Scripts`)
- [ ] Part 2: authentication, double-booking prevention, delete-restriction alerts, Blob Storage image upload, search
- [ ] Part 3: EventType filtering, final deployment

## Key design decisions (Part 1)

These deviate deliberately from the brief's literal ERD sketch — full rationale
lives in the POE report, summarised here so the code makes sense on its own:

- **`Event` has no `VenueId`.** `Booking` is the only place a venue is attached
  to an event, so an event can be loaded before a venue is available.
- **`Event` has `EventStartDate`/`EventEndDate`**, not a single `EventDate`,
  so multi-day events and interval-overlap double-booking checks (Part 2) are
  possible.
- **A single `Event` can technically be booked at more than one `Venue`**
  (e.g. a multi-venue conference) — a natural consequence of `Booking` being
  the join table, treated as an intentional design choice rather than a bug.
  Flagged explicitly in the POE report in case a strict 1:1 reading is expected.
- **`Venue`/`Event` deletion is blocked whenever any `Booking` references
  them** — past or future — enforced at the database level today (both FKs on
  `Booking` default to `ON DELETE NO ACTION`) with a friendly UI alert arriving
  in Part 2.
- **Authentication is scoped to Part 2** (ASP.NET Core Identity, a single
  Admin/BookingSpecialist role), not Part 1 — Part 1 stays a pure CRUD
  foundation with no login screens.

## Prerequisites

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
- SQL Server (LocalDB, SQL Server Express, or a reachable Azure SQL Database)
- (Part 2+) An Azure Storage account for Blob Storage

## Getting started

```bash
git clone https://github.com/realebogamo/EventEaseBooking.git
cd EventEaseBooking   # repo root — EventEaseBooking.sln lives here; every command below assumes this cwd

dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=EventEaseBooking;Trusted_Connection=True;" --project EventEaseBooking.Web

# create and apply the EF Core migration (generates Migrations/ from the models)
dotnet ef migrations add InitialCreate --project EventEaseBooking.Web
dotnet ef database update --project EventEaseBooking.Web

dotnet run --project EventEaseBooking.Web
```

Alternatively, run `Database/Scripts/01_Part1_Schema_And_Seed.sql` directly
against a target database (LocalDB, SQL Server, or Azure SQL) via `sqlcmd` or
Azure Data Studio — it creates the same schema and seed data without EF
migrations.

**Never commit a real connection string or storage key.** `appsettings.json`
ships with an empty `ConnectionStrings:DefaultConnection` on purpose — use
`dotnet user-secrets` locally and Azure App Service Application Settings in
the cloud.

**"The file '...\EventEaseBooking.Web' does not exist" when running a
`dotnet user-secrets`/`dotnet ef` command:** `--project EventEaseBooking.Web`
is a relative path — it's resolved against your current directory. This
error means you're one level above the repo root (e.g. in the folder you
cloned into, rather than inside `EventEaseBooking/`). `cd` into the repo
root first (where `EventEaseBooking.sln` lives) and re-run the command.

### Azure SQL connection resiliency

`Program.cs` enables EF Core's `EnableRetryOnFailure()` on the SQL Server
provider. Azure SQL Database (especially the Basic tier used for this
project) can be slow to respond on the first connection after being idle,
which otherwise surfaces as a `SqlException: Connection Timeout Expired`
on the very first request after a period of inactivity. If you still hit
timeouts, raise `Connect Timeout` in your connection string (e.g. to 60).

## Project structure

```
EventEaseBooking.sln
Database/
  Scripts/01_Part1_Schema_And_Seed.sql   — reference T-SQL schema + seed data
EventEaseBooking.Web/
  Controllers/     VenuesController, EventsController, BookingsController, HomeController
  Models/          Venue, Event, Booking — EF Core entities
  ViewModels/       BookingFormViewModel — backs the Booking create/edit dropdowns
  Data/            ApplicationDbContext
  Views/           Razor views per controller, Bootstrap 5 via CDN
  Program.cs
  appsettings.json — no secrets; connection string set via user-secrets / Azure config
```

No repository/unit-of-work layer: `DbContext` already fills that role for a
project this size — see the Application Structure section of the POE report
for the full justification.

## Deployment

Azure App Service (Basic tier if the Free F1 tier's CPU quota causes issues
during marking) + Azure SQL Database (Basic tier), connection string set via
App Service Application Settings, never in source. Full deployment plan,
resource tiers, and backup strategy are documented in the POE report.
