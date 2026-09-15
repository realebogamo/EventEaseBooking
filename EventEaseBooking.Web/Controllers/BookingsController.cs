using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using EventEaseBooking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class BookingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BookingsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Include(b => b.Venue)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
        return View(bookings);
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
        if (!ModelState.IsValid) return View(await BuildFormViewModel(form));

        var booking = new Booking
        {
            EventId = form.EventId,
            VenueId = form.VenueId,
            BookingDate = DateTime.UtcNow
        };

        _context.Add(booking);
        await _context.SaveChangesAsync();
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
        if (!ModelState.IsValid) return View(await BuildFormViewModel(form));

        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        booking.EventId = form.EventId;
        booking.VenueId = form.VenueId;
        await _context.SaveChangesAsync();

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
}
