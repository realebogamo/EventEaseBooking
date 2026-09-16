using Azure;
using EventEaseBooking.Web.Data;
using EventEaseBooking.Web.Models;
using EventEaseBooking.Web.Services;
using EventEaseBooking.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventEaseBooking.Web.Controllers;

public class VenuesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public VenuesController(ApplicationDbContext context, IBlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    public async Task<IActionResult> Index(VenueFilterViewModel filter)
    {
        var query = _context.Venues.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(v => EF.Functions.Like(v.VenueName, $"%{term}%")
                                   || EF.Functions.Like(v.Location, $"%{term}%"));
        }

        if (filter.MinCapacity is int minCapacity)
            query = query.Where(v => v.Capacity >= minCapacity);

        filter.Results = await query.OrderBy(v => v.VenueName).ToListAsync();
        return View(filter);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == id);
        return venue is null ? NotFound() : View(venue);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("VenueName,Location,Capacity")] Venue venue, IFormFile? imageFile)
    {
        if (imageFile is not null)
        {
            try { _blobStorage.ValidateFile(imageFile); }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }

        if (!ModelState.IsValid) return View(venue);

        // Validated before the row exists (so a bad image never leaves a
        // half-created Venue), uploaded after (so the blob name can carry the
        // real VenueId) — two SaveChanges calls, not one.
        _context.Add(venue);
        await _context.SaveChangesAsync();

        if (imageFile is not null)
        {
            try
            {
                venue.ImageUrl = await _blobStorage.UploadAsync(imageFile, "venues", venue.VenueId);
                await _context.SaveChangesAsync();
            }
            catch (RequestFailedException)
            {
                // The Venue itself is already saved — a storage-side failure
                // (bad config, transient outage) shouldn't 500 the whole request
                // or silently leave the row image-less with no explanation.
                TempData["ImageUploadError"] = "The venue was created, but the image upload failed. You can try again from here.";
                return RedirectToAction(nameof(Edit), new { id = venue.VenueId });
            }
        }

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
    public async Task<IActionResult> Edit(int id, [Bind("VenueId,VenueName,Location,Capacity")] Venue venue, IFormFile? imageFile)
    {
        if (id != venue.VenueId) return NotFound();

        if (imageFile is not null)
        {
            try { _blobStorage.ValidateFile(imageFile); }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }

        if (!ModelState.IsValid) return View(venue);

        var previousImageUrl = await _context.Venues
            .AsNoTracking()
            .Where(v => v.VenueId == id)
            .Select(v => v.ImageUrl)
            .FirstOrDefaultAsync();
        venue.ImageUrl = previousImageUrl;

        if (imageFile is not null)
        {
            try
            {
                venue.ImageUrl = await _blobStorage.UploadAsync(imageFile, "venues", id);
            }
            catch (RequestFailedException ex)
            {
                ModelState.AddModelError(string.Empty, $"Image upload failed: {ex.Message}");
                return View(venue);
            }
        }

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

        // Only delete the old blob after the new URL is safely committed —
        // never leave a Venue pointing at nothing if the save above had failed.
        if (imageFile is not null)
            await _blobStorage.DeleteAsync(previousImageUrl);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == id);
        if (venue is null) return NotFound();

        // Named "active" in the brief, but Booking has no cancelled/completed
        // status — every Booking row is a real occupancy record, so "active"
        // here means "exists at all". Loaded up front so the booking specialist
        // sees exactly which bookings are blocking before they even try to submit.
        ViewBag.BlockingBookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Event)
            .Where(b => b.VenueId == id)
            .OrderBy(b => b.Event!.EventStartDate)
            .ToListAsync();

        return View(venue);
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
            // bookings still exist. This is the safety net for a booking created
            // between the pre-check above and this submit (race condition) — the
            // GET action is what actually shows the alert in the normal case.
            ModelState.AddModelError(string.Empty, "This venue cannot be deleted because it has active bookings.");
            ViewBag.BlockingBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Event)
                .Where(b => b.VenueId == id)
                .OrderBy(b => b.Event!.EventStartDate)
                .ToListAsync();
            return View("Delete", await _context.Venues.AsNoTracking().FirstAsync(v => v.VenueId == id));
        }

        // Best-effort cleanup: the Venue row is already gone, so a storage-side
        // failure here (transient outage, permissions) is an orphaned blob to
        // clean up later, not a reason to fail a delete that already succeeded.
        try { await _blobStorage.DeleteAsync(venue.ImageUrl); }
        catch (RequestFailedException) { }

        return RedirectToAction(nameof(Index));
    }
}
