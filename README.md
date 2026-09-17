# EventEase Booking

Venue booking system for the IIE CLDV7111 Cloud Development Portfolio of Evidence.
Phase 1 is an **internal admin tool** for booking specialists — customers do not
book for themselves; a specialist enters bookings taken by phone, email, or in
person, and can load an event before a venue has been assigned to it.

Built with ASP.NET Core MVC + Entity Framework Core, deployed to Azure App
Service + Azure SQL Database + Azure Blob Storage.

## Project status

### Part 1 — Foundation
- [x] Venue / Event / Booking CRUD (Controllers, Views, EF Core)
- [x] Local SQL schema + seed data script (`Database/Scripts/01_Part1_Schema_And_Seed.sql`)

### Part 2 — Business Rules, Cloud Storage, Search
- [x] Double-booking prevention (venue + overlapping event dates), enforced in
      both the controller and a database trigger
- [x] Delete-restriction alerts on Venue/Event when bookings still reference them
- [x] Image upload to Azure Blob Storage for Venues and Events, replacing the
      Part 1 placeholder URLs
- [x] Consolidated, searchable Bookings view (status badge, filter by
      event/venue name, venue, date range)
- [x] Per-page search on Venues (name/location/capacity) and a global search
      bar (navbar, every page) across Venues, Events, and Bookings
- [ ] Authentication / authorisation for booking specialists — not yet
      implemented; the brief's "authorised personnel" and "securely" language
      is not backed by a login screen yet. Flagged as an open item in the POE
      decisions log rather than assumed away.

### Part 3 — Advanced Filtering
- [x] `EventType` lookup table with predefined categories (Conference,
      Wedding, Concert, Corporate Meeting, Workshop, Exhibition)
- [x] Filtering Events by type, venue, date range, and availability
      ("Needs a venue" vs "Already booked")
- [ ] Final confirmed Azure deployment with all Part 1–3 functionality live
      (CI/CD pipeline exists — see **Deployment** — but has not yet been
      verified end-to-end against a running Azure Web App)

## Key design decisions

These deviate deliberately from the brief's literal ERD sketch — full
rationale lives in the POE report, summarised here so the code makes sense
on its own.

**Part 1**
- **`Event` has no `VenueId`.** `Booking` is the only place a venue is attached
  to an event, so an event can be loaded before a venue is available.
- **`Event` has `EventStartDate`/`EventEndDate`**, not a single `EventDate`,
  so multi-day events and the interval-overlap double-booking check (Part 2)
  are possible.
- **A single `Event` can technically be booked at more than one `Venue`**
  (e.g. a multi-venue conference) — a natural consequence of `Booking` being
  the join table, treated as an intentional design choice rather than a bug.
- **`Venue`/`Event` deletion is blocked whenever any `Booking` references
  them** — past or future — via `ON DELETE NO ACTION`/`Restrict` on both FKs
  on `Booking` (see `ApplicationDbContext`), with a friendly UI alert added
  in Part 2 (see below).

**Part 2**
- **Double booking is enforced twice, on purpose.** `BookingsController`
  checks for an overlap before saving (fast feedback, avoids a DB round trip
  for the common case), but the real guarantee is
  `TR_Booking_PreventOverlap`, an `AFTER INSERT, UPDATE` trigger on `Booking`
  (`Database/Scripts/02_Part2_BusinessRules_And_Images.sql`) — the app-level
  check alone has a race window between two concurrent requests that only a
  DB-level check running in the same transaction can close.
- **`entity.ToTable("Booking", tb => tb.HasTrigger(...))`** in
  `ApplicationDbContext` is required alongside that trigger: SQL Server
  rejects an `OUTPUT` clause on a table with an enabled trigger, and EF Core
  uses `OUTPUT` by default to read back generated values. Without this line,
  every `SaveChangesAsync()` against `Booking` throws
  `SqlException: ... cannot have any enabled triggers if the statement
  contains an OUTPUT clause without INTO clause`, regardless of whether an
  actual overlap occurred. `BookingsController.IsOverlapTriggerError` also
  matches this on SQL error number 334 as a defensive fallback, so a future
  regression here surfaces as a friendly message, not a raw 500.
- **Image URLs are stored directly on `Venue.ImageUrl`/`Event.ImageUrl`**
  (a plain string column), set once at upload time by `BlobStorageService`
  — not resolved by convention at read time. This keeps rendering trivial
  (`<img src="@venue.ImageUrl">`) and correct even if the venue is renamed
  later; the trade-off is that an image manually replaced in the storage
  account outside the app won't be picked up without also updating the row
  (see `Database/Scripts/04_Part2_VenueImageBackfill.sql` for exactly that
  one-time fix).
- **"Availability" for Events (Part 3) is defined against the existing
  schema**: an Event is "available"/needs a venue while it has zero `Booking`
  rows, and "booked" once any `Booking` links it to a venue. This reuses the
  Part 1 rule that `Booking` is the sole record of venue occupancy, and is
  deliberately not the same check as the interval-overlap double-booking
  rule (that one only applies when creating/editing a specific booking).

## Business rules

| Rule | App-level | Database-level |
|---|---|---|
| No venue double-booked for overlapping event dates | `BookingsController.HasOverlapAsync` before save | `TR_Booking_PreventOverlap` trigger, rolls back and raises an error on conflict |
| Venue/Event can't be deleted while a Booking references it | Pre-check in `Delete` GET action shows the blocking bookings | `FK_Booking_Venue`/`FK_Booking_Event` are `ON DELETE NO ACTION` — SQL Server refuses the delete even if the pre-check is bypassed |

Both rules are enforced in the database, not just the UI/controller, because
UI validation alone can't stop a second request that races the first, or a
write made directly against the database outside the app.

## Image handling (Part 2)

- Container `eventease-images` in the configured Storage Account, blob-level
  public read access (images are meant to be viewed in `<img>` tags without
  a SAS token; the storage account itself stays private otherwise).
- App-driven uploads (Create/Edit forms) are named `{folder}/{entityId}-{guid}.{ext}`
  (`venues/…` or `events/…`) via `BlobStorageService.UploadAsync` — collision-proof
  and independent of the entity's display name.
- Replacing an image on Edit deletes the previous blob after the new one is
  saved; deleting a Venue/Event deletes its blob too (best-effort — a storage
  failure here doesn't block a delete that already succeeded in the database).
- File validation (type, size) happens in `BlobStorageService.ValidateFile`
  before any upload is attempted.

## Search and filtering

- **Venues Index** — free-text search (name/location) + minimum capacity.
- **Events Index** (Part 3) — filter by event type, venue, date range, and
  availability; each row shows the booked venue's name and thumbnail.
- **Bookings Index** — free-text search (event/venue name) + venue + date
  range, with an Upcoming/Past status badge.
- **Global search** (navbar, every page) — a single free-text term matched
  against Venues (name/location), Events (name/description), and Bookings
  (via the linked Event's/Venue's name), shown as three grouped result
  tables (`SearchController`, `Views/Search/Index.cshtml`).

## Prerequisites

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
- SQL Server (LocalDB, SQL Server Express, or a reachable Azure SQL Database)
- An Azure Storage account (Blob Storage) — required for image upload to
  work; every other page still works without it (see **Note** below)

## Getting started

```bash
git clone https://github.com/realebogamo/EventEaseBooking.git
cd EventEaseBooking   # repo root — EventEaseBooking.sln lives here; every command below assumes this cwd

dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=EventEaseBooking;Trusted_Connection=True;" --project EventEaseBooking.Web
dotnet user-secrets set "ConnectionStrings:BlobStorage" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net" --project EventEaseBooking.Web

# create and apply the EF Core migration (generates Migrations/ from the models)
dotnet ef migrations add InitialCreate --project EventEaseBooking.Web
dotnet ef database update --project EventEaseBooking.Web

dotnet run --project EventEaseBooking.Web
```

Alternatively, run the scripts in `Database/Scripts/` **in order** (01
through 04) directly against a target database (LocalDB, SQL Server, or
Azure SQL) via `sqlcmd` or Azure Data Studio — this creates the same schema,
seed data, business-rule trigger, and `EventType` lookup table without EF
migrations.

**Note:** `BlobStorageService` is registered as a lazy singleton — if
`ConnectionStrings:BlobStorage` isn't configured, every page that doesn't
touch an image (browsing, filtering, bookings) still works normally; only
an actual image upload will fail. If you fix a missing/incorrect Blob
Storage setting while the app is already running, **restart the app** —
`Lazy<T>`'s default behaviour caches the *first* initialization exception
and keeps rethrowing it for the life of the process, even after the
underlying config is corrected.

**Never commit a real connection string or storage key.** `appsettings.json`
ships with an empty `ConnectionStrings:DefaultConnection` on purpose — use
`dotnet user-secrets` locally and Azure App Service Application Settings
(or Key Vault / managed identity) in the cloud.

**"The file '...\EventEaseBooking.Web' does not exist" when running a
`dotnet user-secrets`/`dotnet ef` command:** `--project EventEaseBooking.Web`
is a relative path — it's resolved against your current directory. This
error means you're one level above the repo root (e.g. in the folder you
cloned into, rather than inside `EventEaseBooking/`). `cd` into the repo
root first (where `EventEaseBooking.sln` lives) and re-run the command.

### Azure SQL connection resiliency

`Program.cs` enables EF Core's `EnableRetryOnFailure()` on the SQL Server
provider. Azure SQL Database (especially the Basic/serverless tiers used for
this project) can be slow to respond on the first connection after being
idle (auto-pause), which otherwise surfaces as a
`SqlException: Connection Timeout Expired` on the very first request after a
period of inactivity. If you still hit timeouts, raise `Connect Timeout` in
your connection string (e.g. to 60).

## Project structure

```
EventEaseBooking.sln
Database/
  Scripts/
    01_Part1_Schema_And_Seed.sql            — Venue/Event/Booking schema + seed data
    02_Part2_BusinessRules_And_Images.sql   — double-booking trigger, Event.ImageUrl column
    03_Part3_EventType_And_Filtering.sql    — EventType lookup table + seed categories
    04_Part2_VenueImageBackfill.sql         — one-time data fix: real venue images
.github/workflows/dotnet.yml                — CI build + auto-deploy to Azure Web App on push to main
EventEaseBooking.Web/
  Controllers/     VenuesController, EventsController, BookingsController, SearchController, HomeController
  Models/          Venue, Event, EventType, Booking — EF Core entities
  ViewModels/      *FilterViewModel (Venues/Events/Bookings Index), GlobalSearchViewModel, BookingFormViewModel
  Services/        IBlobStorageService / BlobStorageService — Azure Blob Storage upload/delete/validation
  Data/            ApplicationDbContext
  Views/           Razor views per controller, Bootstrap 5 via CDN
  Program.cs
  appsettings.json — no secrets; connection strings set via user-secrets / Azure config
```

No repository/unit-of-work layer: `DbContext` already fills that role for a
project this size — see the Application Structure section of the POE report
for the full justification.

## Deployment

`.github/workflows/dotnet.yml` builds on every push/PR to `main`, and on a
successful push to `main` publishes and deploys to an Azure Web App named
`EventEase` via `azure/webapps-deploy`, using the
`AZURE_WEBAPP_PUBLISH_PROFILE` repository secret. This pipeline has not yet
been confirmed to produce a working live site end-to-end — before relying on
it for POE screenshots, verify:

1. The Azure Web App's runtime stack matches the project's target framework
   (`net8.0`).
2. Azure SQL already has the schema applied (run `Database/Scripts/01`
   through `04` against it, or `dotnet ef database update` with the Azure
   connection string) — deploying code does not create tables.
3. The App Service's connection string is named exactly `DefaultConnection`
   (matching `builder.Configuration.GetConnectionString("DefaultConnection")`
   in `Program.cs`) and typed `SQLAzure` — a differently-named entry will not
   be found at runtime and most pages will 500.
4. A `ConnectionStrings:BlobStorage` app setting is present if image upload
   needs to work on the deployed site.

Azure App Service (Basic tier if the Free F1 tier's CPU quota causes issues
during marking) + Azure SQL Database (Basic/serverless tier) + Azure Blob
Storage (Hot tier, LRS). Full deployment plan, resource tiers, and backup
strategy are documented in the POE report.
