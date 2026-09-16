using System.ComponentModel.DataAnnotations;

namespace EventEaseBooking.Web.Models;

// Part 3 lookup table: predefined categories such as Conference, Wedding, Concert.
// It's a reference table, not a business entity with booking history, so its FK
// from Event uses ON DELETE SET NULL (see ApplicationDbContext) rather than the
// Restrict behaviour used for Venue/Event/Booking.
public class EventType
{
    public int EventTypeId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Event Type")]
    public string TypeName { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
