using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventEaseBooking.Web.ViewModels;

// Backs the Booking Create/Edit forms: Event and Venue are picked from dropdowns
// since Booking is the only place a venue is attached to an event.
public class BookingFormViewModel
{
    public int BookingId { get; set; }

    [Required(ErrorMessage = "Please select an event.")]
    [Display(Name = "Event")]
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please select a venue.")]
    [Display(Name = "Venue")]
    public int VenueId { get; set; }

    public IEnumerable<SelectListItem> Events { get; set; } = Enumerable.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> Venues { get; set; } = Enumerable.Empty<SelectListItem>();
}
