using EventEaseBooking.Web.Models;

namespace EventEaseBooking.Web.ViewModels;

// Backs the navbar search box (present on every page via _Layout.cshtml).
// This is a single free-text term across all three entities, distinct from
// the more specific per-entity filter forms already on the Venues/Events/
// Bookings Index pages (name+capacity, type+venue+date+availability,
// name+venue+date respectively) — this one is for "find it, wherever it is".
public class GlobalSearchViewModel
{
    public string? Search { get; set; }

    public IReadOnlyList<Venue> Venues { get; set; } = Array.Empty<Venue>();
    public IReadOnlyList<Event> Events { get; set; } = Array.Empty<Event>();
    public IReadOnlyList<Booking> Bookings { get; set; } = Array.Empty<Booking>();

    public bool HasSearched => !string.IsNullOrWhiteSpace(Search);
    public int TotalResults => Venues.Count + Events.Count + Bookings.Count;
}
