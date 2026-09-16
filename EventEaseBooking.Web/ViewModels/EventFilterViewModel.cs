using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using EventEaseBooking.Web.Models;

namespace EventEaseBooking.Web.ViewModels;

// "Availability" is defined against the current schema, not a hypothetical one:
// an Event is "available" (NeedsVenue) while it has no Booking row at all, and
// "Booked" once any Booking links it to a Venue. This reuses the Part 1 rule
// that Booking is the sole record of venue occupancy — it does not duplicate
// the interval-overlap double-booking check, which lives on Booking creation
// (Part 2), not on this read-only filter.
public enum EventAvailability
{
    [Display(Name = "Any")]
    Any,

    [Display(Name = "Needs a venue")]
    NeedsVenue,

    [Display(Name = "Already booked")]
    Booked
}

// Backs the Events Index page: a GET-bound filter form (so results are a
// shareable/bookmarkable URL) plus the filtered results and the dropdown
// option lists needed to redraw the form.
public class EventFilterViewModel
{
    [Display(Name = "Event Type")]
    public int? EventTypeId { get; set; }

    [Display(Name = "Venue")]
    public int? VenueId { get; set; }

    [Display(Name = "From")]
    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [Display(Name = "To")]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Availability")]
    public EventAvailability Availability { get; set; } = EventAvailability.Any;

    public IEnumerable<SelectListItem> EventTypes { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Venues { get; set; } = Enumerable.Empty<SelectListItem>();
    public IReadOnlyList<Event> Results { get; set; } = Array.Empty<Event>();
}
