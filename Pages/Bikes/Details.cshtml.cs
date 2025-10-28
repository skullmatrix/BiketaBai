using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Bikes;

public class DetailsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BookingService _bookingService;

    public DetailsModel(BiketaBaiDbContext context, BookingService bookingService)
    {
        _context = context;
        _bookingService = bookingService;
    }

    public Bike? Bike { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public List<Rating> Ratings { get; set; } = new();
    public double OwnerRating { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Now.AddHours(1);

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1);

        [Required]
        [MaxLength(255)]
        [Display(Name = "Pickup Location")]
        public string PickupLocation { get; set; } = string.Empty;

        [MaxLength(255)]
        [Display(Name = "Return Location")]
        public string? ReturnLocation { get; set; }

        [Display(Name = "Distance Saved (km)")]
        public decimal? DistanceSavedKm { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Bike = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.Owner)
            .Include(b => b.AvailabilityStatus)
            .Include(b => b.BikeImages)
            .FirstOrDefaultAsync(b => b.BikeId == id);

        if (Bike == null)
            return NotFound();

        // Get ratings
        var ratingValues = await _context.Ratings
            .Where(r => r.BikeId == id)
            .Select(r => r.RatingValue)
            .ToListAsync();

        AverageRating = ratingValues.Any() ? ratingValues.Average() : 0;
        RatingCount = ratingValues.Count;

        Ratings = await _context.Ratings
            .Include(r => r.Rater)
            .Where(r => r.BikeId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Get owner rating
        var ownerRatingValues = await _context.Ratings
            .Where(r => r.RatedUserId == Bike.OwnerId && r.IsRenterRatingOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();

        OwnerRating = ownerRatingValues.Any() ? ownerRatingValues.Average() : 0;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Account/Login");

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!ModelState.IsValid)
        {
            await OnGetAsync(id);
            return Page();
        }

        // If return location is not provided, use pickup location
        var returnLocation = string.IsNullOrWhiteSpace(Input.ReturnLocation) 
            ? Input.PickupLocation 
            : Input.ReturnLocation;

        // Create booking
        var result = await _bookingService.CreateBookingAsync(
            userId.Value,
            id,
            Input.StartDate,
            Input.EndDate,
            Input.DistanceSavedKm,
            Input.PickupLocation,
            returnLocation
        );

        if (result.success)
        {
            return RedirectToPage("/Bookings/Payment", new { bookingId = result.bookingId });
        }
        else
        {
            TempData["ErrorMessage"] = result.message;
            await OnGetAsync(id);
            return Page();
        }
    }
}

