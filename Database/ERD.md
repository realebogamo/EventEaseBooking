# EventEase — Entity Relationship Diagram

Shows how the schema evolves across the three POE parts, not just the final
state. See the decisions log in the POE report for the reasoning behind each
choice noted here.

## Part 1 — Foundation

```mermaid
erDiagram
    VENUE ||--o{ BOOKING : "is booked via"
    EVENT ||--o{ BOOKING : "is booked via"

    VENUE {
        int VenueId PK
        nvarchar VenueName
        nvarchar Location
        int Capacity "CHECK > 0"
        nvarchar ImageUrl "nullable, placeholder URL in Part 1"
    }
    EVENT {
        int EventId PK
        nvarchar EventName
        date EventStartDate
        date EventEndDate "CHECK >= EventStartDate"
        nvarchar Description "nullable"
    }
    BOOKING {
        int BookingId PK
        int EventId FK
        int VenueId FK
        datetime2 BookingDate "audit: when the record was created"
    }
```

**Notes**

- `Event` has no FK to `Venue`. `Booking` is the only place a venue is
  attached to an event, so an event can be loaded before a venue is
  available.
- `Event` carries a start/end date range (not a single `EventDate`) so
  multi-day events and interval-overlap double-booking checks are possible
  from Part 2 onward.
- Both FKs on `Booking` default to `ON DELETE NO ACTION` (Restrict): SQL
  Server itself refuses to delete a `Venue` or `Event` that still has a
  `Booking` row — this is the database-level half of the delete-restriction
  rule, already in place in Part 1.
- A single `Event` can, by this model, be booked at more than one `Venue`
  (e.g. a multi-venue conference) — a natural consequence of `Booking` being
  the join table, treated as an intentional design choice rather than a bug.

## Part 2 — Cloud Storage and Validation (delta only)

```mermaid
erDiagram
    EVENT {
        int EventId PK
        nvarchar EventName
        date EventStartDate
        date EventEndDate
        nvarchar Description "nullable"
        nvarchar ImageUrl "new: nullable, real Blob Storage URL"
    }
```

**Notes**

- `Event.ImageUrl` is added (`Venue.ImageUrl` already existed since Part 1)
  — additive, non-destructive migration.
- No structural change to the Venue/Event/Booking relationships.
- A double-booking trigger is added on `Booking` (see the Business Rules
  section of the POE report) — it enforces the overlap rule but adds no new
  columns or tables.
- ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) are added
  via a standard EF Core Identity migration for the single Admin /
  BookingSpecialist role.

## Part 3 — Advanced Filtering (delta only)

```mermaid
erDiagram
    EVENTTYPE ||--o{ EVENT : categorizes
    EVENTTYPE {
        int EventTypeId PK
        nvarchar TypeName UK "e.g. Conference, Wedding, Concert"
    }
    EVENT {
        int EventTypeId FK "nullable, ON DELETE SET NULL"
    }
```

**Notes**

- `EventType` is a lookup/reference table, not a core business entity with
  booking history, so its FK uses `ON DELETE SET NULL` rather than the
  Restrict behaviour used for Venue/Event/Booking — deleting an unused
  category shouldn't be blocked the way deleting a venue with bookings is.
- `Event.EventTypeId` is nullable; existing Part 1/2 seed events are
  backfilled to a sensible type in the Part 3 migration.

## Full schema (end of Part 3)

```mermaid
erDiagram
    VENUE ||--o{ BOOKING : "is booked via"
    EVENT ||--o{ BOOKING : "is booked via"
    EVENTTYPE ||--o{ EVENT : categorizes

    VENUE {
        int VenueId PK
        nvarchar VenueName
        nvarchar Location
        int Capacity "CHECK > 0"
        nvarchar ImageUrl "nullable"
    }
    EVENT {
        int EventId PK
        nvarchar EventName
        date EventStartDate
        date EventEndDate "CHECK >= EventStartDate"
        nvarchar Description "nullable"
        nvarchar ImageUrl "nullable, Part 2+"
        int EventTypeId FK "nullable, Part 3, ON DELETE SET NULL"
    }
    BOOKING {
        int BookingId PK
        int EventId FK "ON DELETE NO ACTION"
        int VenueId FK "ON DELETE NO ACTION"
        datetime2 BookingDate "audit timestamp"
    }
    EVENTTYPE {
        int EventTypeId PK
        nvarchar TypeName UK
    }
```
