/*
    EventEase Booking — Part 2 delta: business rules + image storage column.
    Run after 01_Part1_Schema_And_Seed.sql, before 03_Part3_EventType_And_Filtering.sql.

    Design notes (see POE report for full rationale):
    - The double-booking rule ("same venue, overlapping event dates") is
      enforced here at the DB level via an AFTER INSERT/UPDATE trigger, on top
      of the app-level check in BookingsController. The app-level check alone
      has a race window between two concurrent requests; the trigger is what
      actually guarantees no overlap can ever be committed, because it runs
      inside the same transaction as the write that would create one.
    - Event carries only a date (no time-of-day), so two bookings at the same
      venue whose event date ranges merely touch on the same day count as
      overlapping — there is no time-of-day data to prove otherwise.
    - Event.ImageUrl is additive and nullable: existing rows (and the Part 1
      placeholder URLs) are untouched until the app replaces them via a real
      Blob Storage upload.
*/

ALTER TABLE dbo.Event ADD ImageUrl NVARCHAR(500) NULL;
GO

CREATE TRIGGER dbo.TR_Booking_PreventOverlap
ON dbo.Booking
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Event ei ON ei.EventId = i.EventId
        JOIN dbo.Booking b ON b.VenueId = i.VenueId AND b.BookingId <> i.BookingId
        JOIN dbo.Event eb ON eb.EventId = b.EventId
        WHERE ei.EventStartDate <= eb.EventEndDate
          AND ei.EventEndDate   >= eb.EventStartDate
    )
    BEGIN
        RAISERROR ('This venue already has an overlapping booking for the selected event dates.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END
GO
