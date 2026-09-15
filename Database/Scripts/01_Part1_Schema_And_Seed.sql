/*
    EventEase Booking — Part 1 schema and seed data.
    Run against a fresh LocalDB / Azure SQL database.

    Design notes (see POE report for full rationale):
    - Event has no VenueId: an event can exist before a venue is assigned.
      Booking is the sole record of which venue an event occupies.
    - Event carries a start/end date range (not a single date) so multi-day
      events and interval-overlap double-booking checks are possible from Part 2.
    - Both FKs on Booking default to ON DELETE NO ACTION (Restrict): SQL Server
      itself refuses to delete a Venue or Event that still has a Booking row.
*/

CREATE TABLE dbo.Venue (
    VenueId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Venue PRIMARY KEY,
    VenueName   NVARCHAR(200)     NOT NULL,
    Location    NVARCHAR(300)     NOT NULL,
    Capacity    INT               NOT NULL CONSTRAINT CK_Venue_Capacity CHECK (Capacity > 0),
    ImageUrl    NVARCHAR(500)     NULL
);

CREATE TABLE dbo.Event (
    EventId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Event PRIMARY KEY,
    EventName       NVARCHAR(200)     NOT NULL,
    EventStartDate  DATE              NOT NULL,
    EventEndDate    DATE              NOT NULL,
    Description     NVARCHAR(1000)    NULL,
    CONSTRAINT CK_Event_DateRange CHECK (EventEndDate >= EventStartDate)
);

CREATE TABLE dbo.Booking (
    BookingId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Booking PRIMARY KEY,
    EventId     INT          NOT NULL,
    VenueId     INT          NOT NULL,
    BookingDate DATETIME2(0) NOT NULL CONSTRAINT DF_Booking_BookingDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Booking_Event FOREIGN KEY (EventId) REFERENCES dbo.Event(EventId),
    CONSTRAINT FK_Booking_Venue FOREIGN KEY (VenueId) REFERENCES dbo.Venue(VenueId)
);

CREATE INDEX IX_Booking_VenueId  ON dbo.Booking(VenueId);
CREATE INDEX IX_Booking_EventId  ON dbo.Booking(EventId);
CREATE INDEX IX_Event_DateRange  ON dbo.Event(EventStartDate, EventEndDate);
GO

INSERT INTO dbo.Venue (VenueName, Location, Capacity, ImageUrl) VALUES
('Riverside Conference Hall', '12 River Road, Johannesburg', 250, 'https://placehold.co/600x400?text=Riverside+Hall'),
('Oakwood Function Centre',   '45 Oak Street, Pretoria',      120, 'https://placehold.co/600x400?text=Oakwood+Centre'),
('Harbour View Ballroom',     '8 Harbour Drive, Cape Town',   400, 'https://placehold.co/600x400?text=Harbour+View'),
('Summit Boardroom',          '3 Summit Ave, Sandton',         30, 'https://placehold.co/600x400?text=Summit+Boardroom');

INSERT INTO dbo.Event (EventName, EventStartDate, EventEndDate, Description) VALUES
('Tech Innovators Summit',  '2026-11-10', '2026-11-11', 'Two-day conference on emerging tech.'),
('Smith-Naidoo Wedding',    '2026-10-03', '2026-10-03', 'Wedding reception, evening event.'),
('Acoustic Nights Concert', '2026-12-05', '2026-12-05', 'Live acoustic music evening — awaiting venue assignment.'),
('Regional Sales Kickoff',  '2026-09-28', '2026-09-28', 'Loaded before a venue was available.');

INSERT INTO dbo.Booking (EventId, VenueId) VALUES
(1, 1),  -- Tech Innovators Summit @ Riverside Conference Hall
(2, 3);  -- Smith-Naidoo Wedding @ Harbour View Ballroom
-- Events 3 and 4 deliberately have no Booking row: proves an event can exist before a venue is assigned.
GO
