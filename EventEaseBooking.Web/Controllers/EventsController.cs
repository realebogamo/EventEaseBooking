using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;

    public EventsController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
        => View(await _context.Events.AsNoTracking().OrderBy(e => e.EventStartDate).ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        return @event is null ? NotFound() : View(@event);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("EventName,EventStartDate,EventEndDate,Description")] Event @event)
    {
        if (!ModelState.IsValid) return View(@event);

        _context.Add(@event);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.FindAsync(id);
        return @event is null ? NotFound() : View(@event);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("EventId,EventName,EventStartDate,EventEndDate,Description")] Event @event)
    {
        if (id != @event.EventId) return NotFound();
        if (!ModelState.IsValid) return View(@event);

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

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var @event = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        return @event is null ? NotFound() : View(@event);
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
            // bookings still exist. Part 2 replaces this generic catch with a
            // pre-check that names the blocking bookings before the user even submits.
            ModelState.AddModelError(string.Empty, "This event cannot be deleted because it has existing bookings.");
            return View("Delete", @event);
        }

        return RedirectToAction(nameof(Index));
    }
}
