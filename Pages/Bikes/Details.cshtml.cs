using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;
using Serilog;

namespace BiketaBai.Pages.Bikes;

public class DetailsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BookingService _bookingService;
    private readonly OtpService _otpService;

    public DetailsModel(BiketaBaiDbContext context, BookingService bookingService, OtpService otpService)
    {
        _context = context;
        _bookingService = bookingService;
        _otpService = otpService;
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

        // Note: Phone verification is now handled on a separate page
        // If user needs verification, they'll be redirected when trying to book

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

        // Check if this is first-time rental and requires phone verification
        var user = await _context.Users.FindAsync(userId.Value);
        if (user != null)
        {
            // Check if user has any completed bookings
            var hasCompletedBooking = await _context.Bookings
                .AnyAsync(b => b.RenterId == userId.Value && b.BookingStatusId == 3); // 3 = Completed

            if (!hasCompletedBooking)
            {
                // First-time renter - check if they have a phone number
                if (string.IsNullOrWhiteSpace(user.Phone))
                {
                    TempData["ErrorMessage"] = "Please add your phone number in your profile before making your first booking. Phone verification is required for first-time renters.";
                    return RedirectToPage("/Account/Profile");
                }

                // Check if phone is already verified (check for verified OTP)
                var hasVerifiedOtp = await _context.PhoneOtps
                    .AnyAsync(o => o.PhoneNumber == user.Phone && o.IsVerified && o.VerifiedAt.HasValue);

                if (!hasVerifiedOtp)
                {
                    // Redirect to phone verification page with return URL
                    var returnUrl = $"/Bikes/Details/{id}";
                    TempData["ErrorMessage"] = "Please verify your phone number to complete your first booking.";
                    return RedirectToPage("/Account/VerifyPhone", new { returnUrl });
                }
            }
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
            await LoadBikeDataAsync(id);
            return Page();
        }

        // Create booking using BookingService (doesn't process payment)
        var result = await _bookingService.CreateBookingAsync(
            userId.Value,
            id,
            Input.StartDate.Value,
            Input.EndDate.Value
        );

        if (result.success)
        {
            // Redirect to payment page with the booking ID
            return RedirectToPage("/Bookings/Payment", new { bookingId = result.bookingId });
        }
        else
        {
            TempData["ErrorMessage"] = result.message;
            // Dates are preserved in Input model, no need to reload
            return Page();
        }
    }
}

