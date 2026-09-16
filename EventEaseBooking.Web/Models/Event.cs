using System.ComponentModel.DataAnnotations;

namespace EventEaseBooking.Web.Models;

// No VenueId here by design: an event can be loaded before a venue is assigned.
// Booking is the only place venue <-> event <-> date is recorded (see POE decisions log).
public class Event : IValidatableObject
{
    public int EventId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Event Name")]
    public string EventName { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime EventStartDate { get; set; } = DateTime.Today;

    [Required, DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EventEndDate { get; set; } = DateTime.Today;

    [StringLength(1000)]
    public string? Description { get; set; }

    // Part 3: nullable so existing/new events can stay uncategorised, and so
    // deleting an EventType (ON DELETE SET NULL) never blocks or cascades.
    [Display(Name = "Event Type")]
    public int? EventTypeId { get; set; }
    public EventType? EventType { get; set; }

    // Part 2: real Blob Storage URL, set by BlobStorageService after upload —
    // never bound directly from a form post (see EventsController).
    [StringLength(500)]
    [Display(Name = "Image URL")]
    public string? ImageUrl { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EventEndDate < EventStartDate)
        {
            yield return new ValidationResult(
                "End date cannot be before the start date.",
                new[] { nameof(EventEndDate) });
        }
    }
}
