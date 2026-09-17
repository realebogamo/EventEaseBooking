/*
    EventEase Booking — Part 2 data fix: point existing Venue rows at the
    real images already uploaded to Blob Storage (container eventease-images),
    replacing the Part 1 placehold.co URLs.

    These blobs were uploaded directly to the storage account (not through the
    app's Create/Edit upload flow), using the venue's display name as the blob
    name instead of the app's own "{id}-{guid}.{ext}" convention. Two blob
    names contain minor spelling differences from the Venue.VenueName they
    belong to ("Reverside" vs "Riverside", "Center" vs "Centre") — the mapping
    below is by VenueId, not by a name-matching query, so those differences
    don't matter here, but keep them in mind if you rename a blob later.

    This is a one-time data fix, not a repeatable migration: rerunning it is
    harmless (it just re-sets the same values), but it will NOT pick up future
    blob renames or new venues automatically. VenueId 5 ("The Venue") was
    created directly through the live app, not by the Part 1 seed script.
*/

UPDATE dbo.Venue SET ImageUrl = 'https://eventbasestorageblob.blob.core.windows.net/eventease-images/Reverside%20Conference%20Hall.jpeg' WHERE VenueId = 1; -- Riverside Conference Hall
UPDATE dbo.Venue SET ImageUrl = 'https://eventbasestorageblob.blob.core.windows.net/eventease-images/Oakwood%20Function%20Center.webp'    WHERE VenueId = 2; -- Oakwood Function Centre
UPDATE dbo.Venue SET ImageUrl = 'https://eventbasestorageblob.blob.core.windows.net/eventease-images/Harbour%20View%20Ballroom.jpeg'      WHERE VenueId = 3; -- Harbour View Ballroom
UPDATE dbo.Venue SET ImageUrl = 'https://eventbasestorageblob.blob.core.windows.net/eventease-images/Summit%20Boardroom.jpeg'             WHERE VenueId = 4; -- Summit Boardroom
UPDATE dbo.Venue SET ImageUrl = 'https://eventbasestorageblob.blob.core.windows.net/eventease-images/The%20Venue.jpeg'                    WHERE VenueId = 5; -- The Venue
GO
