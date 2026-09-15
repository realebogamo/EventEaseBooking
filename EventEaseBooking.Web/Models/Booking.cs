using System.ComponentModel.DataAnnotations;

namespace EventEaseBooking.Web.Models;

// The associative entity: pairs one Venue with one Event on one occupancy record.
// BookingDate is an audit timestamp (when the record was created), not the occupied
// date range — the occupied range comes from the linked Event's Start/End dates.
public class Booking
{
    public int BookingId { get; set; }

    [Required]
    public int EventId { get; set; }
    public Event? Event { get; set; }

    [Required]
    public int VenueId { get; set; }
    public Venue? Venue { get; set; }

    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
}
