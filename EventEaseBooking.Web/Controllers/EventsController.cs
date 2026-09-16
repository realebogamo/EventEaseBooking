using Azure;
using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using EventEaseBooking.Web.Services;
using EventEaseBooking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public EventsController(ApplicationDbContext context, IBlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    // Bound from the query string (GET) so a filtered view is a shareable URL.
    public async Task<IActionResult> Index(EventFilterViewModel filter)
    {
        var query = _context.Events
            .AsNoTracking()
            .Include(e => e.EventType)
            .Include(e => e.Bookings).ThenInclude(b => b.Venue)
            .AsQueryable();

        if (filter.EventTypeId is int eventTypeId)
            query = query.Where(e => e.EventTypeId == eventTypeId);

        if (filter.VenueId is int venueId)
            query = query.Where(e => e.Bookings.Any(b => b.VenueId == venueId));

        // Overlap, not equality: an event matches a date range if any part of its
        // Start-End span falls within it, consistent with Event carrying a range.
        if (filter.StartDate is DateTime startDate)
            query = query.Where(e => e.EventEndDate >= startDate);

        if (filter.EndDate is DateTime endDate)
            query = query.Where(e => e.EventStartDate <= endDate);

        query = filter.Availability switch
        {
            EventAvailability.NeedsVenue => query.Where(e => !e.Bookings.Any()),
            EventAvailability.Booked => query.Where(e => e.Bookings.Any()),
            _ => query
        };

        filter.Results = await query.OrderBy(e => e.EventStartDate).ToListAsync();
        filter.EventTypes = await BuildEventTypeOptions();
        filter.Venues = await BuildVenueOptions();

        return View(filter);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.AsNoTracking().Include(e => e.EventType).FirstOrDefaultAsync(e => e.EventId == id);
        return @event is null ? NotFound() : View(@event);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.EventTypes = await BuildEventTypeOptions();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EventName,EventStartDate,EventEndDate,Description,EventTypeId")] Event @event, IFormFile? imageFile)
    {
        if (imageFile is not null)
        {
            try { _blobStorage.ValidateFile(imageFile); }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.EventTypes = await BuildEventTypeOptions();
            return View(@event);
        }

        _context.Add(@event);
        await _context.SaveChangesAsync();

        if (imageFile is not null)
        {
            try
            {
                @event.ImageUrl = await _blobStorage.UploadAsync(imageFile, "events", @event.EventId);
                await _context.SaveChangesAsync();
            }
            catch (RequestFailedException)
            {
                TempData["ImageUploadError"] = "The event was created, but the image upload failed. You can try again from here.";
                return RedirectToAction(nameof(Edit), new { id = @event.EventId });
            }
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.FindAsync(id);
        if (@event is null) return NotFound();

        ViewBag.EventTypes = await BuildEventTypeOptions();
        return View(@event);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("EventId,EventName,EventStartDate,EventEndDate,Description,EventTypeId")] Event @event, IFormFile? imageFile)
    {
        if (id != @event.EventId) return NotFound();

        if (imageFile is not null)
        {
            try { _blobStorage.ValidateFile(imageFile); }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.EventTypes = await BuildEventTypeOptions();
            return View(@event);
        }

        var previousImageUrl = await _context.Events
            .AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => e.ImageUrl)
            .FirstOrDefaultAsync();
        @event.ImageUrl = previousImageUrl;

        if (imageFile is not null)
        {
            try
            {
                @event.ImageUrl = await _blobStorage.UploadAsync(imageFile, "events", id);
            }
            catch (RequestFailedException ex)
            {
                ModelState.AddModelError(string.Empty, $"Image upload failed: {ex.Message}");
                ViewBag.EventTypes = await BuildEventTypeOptions();
                return View(@event);
            }
        }

        try
        {
            _context.Update(@event);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Events.AnyAsync(e => e.EventId == id)) return NotFound();
            throw;
        }

        if (imageFile is not null)
            await _blobStorage.DeleteAsync(previousImageUrl);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (@event is null) return NotFound();

        // See VenuesController.Delete for why "active" means "exists at all"
        // against the current Booking schema.
        ViewBag.BlockingBookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Venue)
            .Where(b => b.EventId == id)
            .OrderBy(b => b.Venue!.VenueName)
            .ToListAsync();

        return View(@event);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var @event = await _context.Events.FindAsync(id);
        if (@event is null) return NotFound();

        try
        {
            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // FK_Booking_Event is ON DELETE NO ACTION: SQL Server rejects this when
            // bookings still exist — the safety net for a booking created between
            // the pre-check above and this submit (race condition).
            ModelState.AddModelError(string.Empty, "This event cannot be deleted because it has active bookings.");
            ViewBag.BlockingBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Venue)
                .Where(b => b.EventId == id)
                .OrderBy(b => b.Venue!.VenueName)
                .ToListAsync();
            return View("Delete", await _context.Events.AsNoTracking().FirstAsync(e => e.EventId == id));
        }

        // Best-effort cleanup: the Event row is already gone, so a storage-side
        // failure here is an orphaned blob to clean up later, not a reason to
        // fail a delete that already succeeded.
        try { await _blobStorage.DeleteAsync(@event.ImageUrl); }
        catch (RequestFailedException) { }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> BuildEventTypeOptions()
        => await _context.EventTypes
            .OrderBy(t => t.TypeName)
            .Select(t => new SelectListItem(t.TypeName, t.EventTypeId.ToString()))
            .ToListAsync();

    private async Task<IEnumerable<SelectListItem>> BuildVenueOptions()
        => await _context.Venues
            .OrderBy(v => v.VenueName)
            .Select(v => new SelectListItem(v.VenueName, v.VenueId.ToString()))
            .ToListAsync();
}
