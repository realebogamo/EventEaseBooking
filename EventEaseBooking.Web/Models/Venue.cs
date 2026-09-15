using System.ComponentModel.DataAnnotations;

namespace EventEaseBooking.Web.Models;

public class Venue
{
    public int VenueId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Venue Name")]
    public string VenueName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Location { get; set; } = string.Empty;

    [Required, Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
    public int Capacity { get; set; }

    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
