using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

// Backs the navbar search box in _Layout.cshtml, present on every page.
// Read-only triage across all three entities by a single free-text term;
// each result links out to that entity's own controller for Details/Edit/Delete.
public class SearchController : Controller
{
    private readonly ApplicationDbContext _context;

    public SearchController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? q)
    {
        var model = new GlobalSearchViewModel { Search = q };
        if (string.IsNullOrWhiteSpace(q))
            return View(model);

        var term = q.Trim();

        model.Venues = await _context.Venues
            .AsNoTracking()
            .Where(v => EF.Functions.Like(v.VenueName, $"%{term}%") || EF.Functions.Like(v.Location, $"%{term}%"))
            .OrderBy(v => v.VenueName)
            .ToListAsync();

        model.Events = await _context.Events
            .AsNoTracking()
            .Include(e => e.EventType)
            .Where(e => EF.Functions.Like(e.EventName, $"%{term}%")
                     || (e.Description != null && EF.Functions.Like(e.Description, $"%{term}%")))
            .OrderBy(e => e.EventStartDate)
            .ToListAsync();

        model.Bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Include(b => b.Venue)
            .Where(b => EF.Functions.Like(b.Event!.EventName, $"%{term}%") || EF.Functions.Like(b.Venue!.VenueName, $"%{term}%"))
            .OrderBy(b => b.Event!.EventStartDate)
            .ToListAsync();

        return View(model);
    }
}
