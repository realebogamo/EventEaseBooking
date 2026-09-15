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
