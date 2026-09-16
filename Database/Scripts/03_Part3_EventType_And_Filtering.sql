/*
    EventEase Booking — Part 3 delta: EventType lookup table.
    Run after 01_Part1_Schema_And_Seed.sql, against the same database.

    Design notes (see POE report / Database/ERD.md for full rationale):
    - EventType is a lookup/reference table, not a business entity with booking
      history, so its FK from Event uses ON DELETE SET NULL rather than the
      Restrict behaviour used for the Venue/Event/Booking relationships.
    - Event.EventTypeId is nullable: existing (and future) events are not
      forced to carry a category, and deleting a category never deletes or
      blocks deletion of the events that used it.
    - This script only adds a table and a nullable column — additive, so it
      does not require touching existing Venue/Event/Booking rows beyond the
      one-time backfill below.
*/

CREATE TABLE dbo.EventType (
    EventTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventType PRIMARY KEY,
    TypeName    NVARCHAR(100)     NOT NULL,
    CONSTRAINT UQ_EventType_TypeName UNIQUE (TypeName)
);

ALTER TABLE dbo.Event
    ADD EventTypeId INT NULL
        CONSTRAINT FK_Event_EventType FOREIGN KEY REFERENCES dbo.EventType(EventTypeId)
        ON DELETE SET NULL;

CREATE INDEX IX_Event_EventTypeId ON dbo.Event(EventTypeId);
GO

INSERT INTO dbo.EventType (TypeName) VALUES
('Conference'),
('Wedding'),
('Concert'),
('Corporate Meeting'),
('Workshop'),
('Exhibition');
GO

-- Backfill the Part 1 seed events with a sensible category. Matches the
-- EventTypeId values EF Core's HasData seed uses for the same four events.
UPDATE dbo.Event SET EventTypeId = (SELECT EventTypeId FROM dbo.EventType WHERE TypeName = 'Conference')        WHERE EventName = 'Tech Innovators Summit';
UPDATE dbo.Event SET EventTypeId = (SELECT EventTypeId FROM dbo.EventType WHERE TypeName = 'Wedding')           WHERE EventName = 'Smith-Naidoo Wedding';
UPDATE dbo.Event SET EventTypeId = (SELECT EventTypeId FROM dbo.EventType WHERE TypeName = 'Concert')           WHERE EventName = 'Acoustic Nights Concert';
UPDATE dbo.Event SET EventTypeId = (SELECT EventTypeId FROM dbo.EventType WHERE TypeName = 'Corporate Meeting') WHERE EventName = 'Regional Sales Kickoff';
GO
