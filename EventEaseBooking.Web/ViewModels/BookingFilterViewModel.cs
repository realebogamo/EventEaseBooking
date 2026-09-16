using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using EventEaseBooking.Web.Models;

namespace EventEaseBooking.Web.ViewModels;

// Backs the consolidated Bookings Index page: a GET-bound search over event/
// venue name plus a venue filter and a date range against the linked Event's
// date range (Booking itself carries no occupied-date range, see Booking.cs).
public class BookingFilterViewModel
{
    [Display(Name = "Search")]
    public string? Search { get; set; }

    [Display(Name = "Venue")]
    public int? VenueId { get; set; }

    [Display(Name = "From")]
    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [Display(Name = "To")]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    public IEnumerable<SelectListItem> Venues { get; set; } = Enumerable.Empty<SelectListItem>();
    public IReadOnlyList<Booking> Results { get; set; } = Array.Empty<Booking>();
}
