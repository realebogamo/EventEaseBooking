using System.ComponentModel.DataAnnotations;
using EventEaseBooking.Web.Models;

namespace EventEaseBooking.Web.ViewModels;

// Backs the Venues Index page: a GET-bound search form over name/location text
// and a minimum capacity, plus the filtered results.
public class VenueFilterViewModel
{
    [Display(Name = "Search")]
    public string? Search { get; set; }

    [Display(Name = "Min Capacity")]
    [Range(1, int.MaxValue, ErrorMessage = "Minimum capacity must be greater than 0.")]
    public int? MinCapacity { get; set; }

    public IReadOnlyList<Venue> Results { get; set; } = Array.Empty<Venue>();
}
