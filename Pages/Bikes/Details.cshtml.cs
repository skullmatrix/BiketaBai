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
    private readonly BookingManagementService _bookingService;

    public DetailsModel(BiketaBaiDbContext context, BookingManagementService bookingService)
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
        public DateTime? StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }
    }

    private async Task LoadBikeDataAsync(int id)
    {
        Bike = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.Owner)
            .Include(b => b.AvailabilityStatus)
            .Include(b => b.BikeImages)
            .FirstOrDefaultAsync(b => b.BikeId == id);

        if (Bike == null)
            return;

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
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadBikeDataAsync(id);

        if (Bike == null)
            return NotFound();

        // Initialize default rental dates only if not already set
        if (!Input.StartDate.HasValue)
            Input.StartDate = DateTime.Now.AddHours(1);
        if (!Input.EndDate.HasValue)
            Input.EndDate = DateTime.Now.AddDays(1);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Bikes/Details/{id}" });

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Bikes/Details/{id}" });

        // Load bike data first (preserves Input dates)
        await LoadBikeDataAsync(id);
        
        if (Bike == null)
            return NotFound();

        // Ensure user is a renter
        if (!AuthHelper.IsRenter(User))
        {
            TempData["ErrorMessage"] = "You need to be registered as a renter to book bikes. Please update your profile.";
            return Page();
        }

        // Validate dates
        if (!Input.StartDate.HasValue || !Input.EndDate.HasValue)
        {
            TempData["ErrorMessage"] = "Please select both start and end dates";
            return Page();
        }

        // Validate that end date is after start date
        if (Input.EndDate.Value <= Input.StartDate.Value)
        {
            TempData["ErrorMessage"] = "End date must be after start date";
            return Page();
        }

        // Validate that start date is in the future
        if (Input.StartDate.Value < DateTime.Now)
        {
            TempData["ErrorMessage"] = "Start date cannot be in the past";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Create booking using BookingManagementService
        var result = await _bookingService.CreateBookingAsync(
            userId.Value,
            id,
            Input.StartDate.Value,
            Input.EndDate.Value
        );

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Dashboard/Home");
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
            // Dates are preserved in Input model, no need to reload
            return Page();
        }
    }
}

