using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class VenuesController : Controller
{
    private readonly ApplicationDbContext _context;

    public VenuesController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
        => View(await _context.Venues.AsNoTracking().OrderBy(v => v.VenueName).ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == id);
        return venue is null ? NotFound() : View(venue);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("VenueName,Location,Capacity,ImageUrl")] Venue venue)
    {
        if (!ModelState.IsValid) return View(venue);

        _context.Add(venue);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var venue = await _context.Venues.FindAsync(id);
        return venue is null ? NotFound() : View(venue);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("VenueId,VenueName,Location,Capacity,ImageUrl")] Venue venue)
    {
        if (id != venue.VenueId) return NotFound();
        if (!ModelState.IsValid) return View(venue);

        try
        {
            _context.Update(venue);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Venues.AnyAsync(v => v.VenueId == id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == id);
        return venue is null ? NotFound() : View(venue);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var venue = await _context.Venues.FindAsync(id);
        if (venue is null) return NotFound();

        try
        {
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // FK_Booking_Venue is ON DELETE NO ACTION: SQL Server rejects this when
            // bookings still exist. Part 2 replaces this generic catch with a
            // pre-check that names the blocking bookings before the user even submits.
            ModelState.AddModelError(string.Empty, "This venue cannot be deleted because it has existing bookings.");
            return View("Delete", venue);
        }

        return RedirectToAction(nameof(Index));
    }
}
