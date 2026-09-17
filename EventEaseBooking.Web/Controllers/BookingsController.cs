using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using EventEaseBooking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class BookingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BookingsController(ApplicationDbContext context) => _context = context;

    // Consolidated view: searchable by event/venue name, filterable by venue and
    // by the linked Event's date range, ordered by event date (not BookingDate)
    // so the booking specialist sees what's actually coming up next.
    public async Task<IActionResult> Index(BookingFilterViewModel filter)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Include(b => b.Venue)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(b => EF.Functions.Like(b.Event!.EventName, $"%{term}%")
                                   || EF.Functions.Like(b.Venue!.VenueName, $"%{term}%"));
        }

        if (filter.VenueId is int venueId)
            query = query.Where(b => b.VenueId == venueId);

        if (filter.StartDate is DateTime startDate)
            query = query.Where(b => b.Event!.EventEndDate >= startDate);

        if (filter.EndDate is DateTime endDate)
            query = query.Where(b => b.Event!.EventStartDate <= endDate);

        filter.Results = await query.OrderBy(b => b.Event!.EventStartDate).ToListAsync();
        filter.Venues = await _context.Venues
            .OrderBy(v => v.VenueName)
            .Select(v => new SelectListItem(v.VenueName, v.VenueId.ToString()))
            .ToListAsync();

        return View(filter);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Include(b => b.Venue)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        return booking is null ? NotFound() : View(booking);
    }

    public async Task<IActionResult> Create()
        => View(await BuildFormViewModel(new BookingFormViewModel()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingFormViewModel form)
    {
        if (ModelState.IsValid && await HasOverlapAsync(form.VenueId, form.EventId, excludeBookingId: null))
            ModelState.AddModelError(string.Empty, "This venue already has a booking that overlaps the selected event's dates.");

        if (!ModelState.IsValid) return View(await BuildFormViewModel(form));

        var booking = new Booking
        {
            EventId = form.EventId,
            VenueId = form.VenueId,
            BookingDate = DateTime.UtcNow
        };

        try
        {
            _context.Add(booking);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsOverlapTriggerError(ex))
        {
            // Belt-and-braces: the TR_Booking_PreventOverlap trigger catches a
            // conflict created by a concurrent request between our check above
            // and this save — the race the app-level check alone can't close.
            ModelState.AddModelError(string.Empty, "This venue already has a booking that overlaps the selected event's dates.");
            return View(await BuildFormViewModel(form));
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        var form = new BookingFormViewModel
        {
            BookingId = booking.BookingId,
            EventId = booking.EventId,
            VenueId = booking.VenueId
        };
        return View(await BuildFormViewModel(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookingFormViewModel form)
    {
        if (id != form.BookingId) return NotFound();

        if (ModelState.IsValid && await HasOverlapAsync(form.VenueId, form.EventId, excludeBookingId: id))
            ModelState.AddModelError(string.Empty, "This venue already has a booking that overlaps the selected event's dates.");

        if (!ModelState.IsValid) return View(await BuildFormViewModel(form));

        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        booking.EventId = form.EventId;
        booking.VenueId = form.VenueId;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsOverlapTriggerError(ex))
        {
            ModelState.AddModelError(string.Empty, "This venue already has a booking that overlaps the selected event's dates.");
            return View(await BuildFormViewModel(form));
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Include(b => b.Venue)
            .FirstOrDefaultAsync(b => b.BookingId == id);
        return booking is null ? NotFound() : View(booking);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<BookingFormViewModel> BuildFormViewModel(BookingFormViewModel form)
    {
        form.Events = await _context.Events
            .OrderBy(e => e.EventName)
            .Select(e => new SelectListItem($"{e.EventName} ({e.EventStartDate:d})", e.EventId.ToString()))
            .ToListAsync();

        form.Venues = await _context.Venues
            .OrderBy(v => v.VenueName)
            .Select(v => new SelectListItem(v.VenueName, v.VenueId.ToString()))
            .ToListAsync();

        return form;
    }

    // App-level half of the double-booking rule. Event carries only a date (no
    // time-of-day), so two events at the same venue that merely touch the same
    // day count as overlapping — there's no way to prove they don't clash.
    // excludeBookingId lets Edit compare a booking against every OTHER booking
    // without tripping over itself.
    private async Task<bool> HasOverlapAsync(int venueId, int eventId, int? excludeBookingId)
    {
        var target = await _context.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => new { e.EventStartDate, e.EventEndDate })
            .FirstOrDefaultAsync();
        if (target is null) return false;

        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Where(b => b.VenueId == venueId && (excludeBookingId == null || b.BookingId != excludeBookingId))
            .AnyAsync(b => b.Event!.EventStartDate <= target.EventEndDate && b.Event.EventEndDate >= target.EventStartDate);
    }

    // This check-then-insert has a race window between two concurrent requests;
    // TR_Booking_PreventOverlap (see Database/Scripts/02_Part2_BusinessRules_And_Images.sql)
    // is the real guarantee. Error 50000 is the trigger's own RAISERROR text.
    // Error 334 ("OUTPUT clause without INTO on a table with enabled triggers")
    // is caught too: Booking's only trigger is the overlap check, so if this
    // table config (entity.ToTable(tb => tb.HasTrigger(...)) in
    // ApplicationDbContext) is ever removed or a migration recreates the table
    // without it, callers still see a friendly message instead of a raw 500.
    private static bool IsOverlapTriggerError(DbUpdateException ex)
        => ex.InnerException is SqlException sqlEx
           && (sqlEx.Number == 334 || sqlEx.Message.Contains("overlapping booking", StringComparison.OrdinalIgnoreCase));
}
