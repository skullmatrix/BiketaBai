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

        // No need to initialize dates - they will be set from hours input on form submission
        // Booking always starts immediately (current time)

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
                    // Store booking details in TempData before redirecting
                    // Get quantity and hours from form first
                    int quantity = 1;
                    decimal rentalHours = 2;
                    
                    if (Request.Form.ContainsKey("quantity") && int.TryParse(Request.Form["quantity"], out var qty))
                        quantity = qty;
                    
                    if (Request.Form.ContainsKey("rentalHours") && decimal.TryParse(Request.Form["rentalHours"], out var hours))
                        rentalHours = hours;
                    
                    // Store in TempData
                    TempData["BookingQuantity"] = quantity.ToString();
                    TempData["BookingHours"] = rentalHours.ToString();
                    TempData["BikeId"] = id.ToString();
                    
                    // Redirect to phone verification page with return URL
                    var returnUrl = $"/Bikes/Details/{id}";
                    TempData["ErrorMessage"] = "Please verify your phone number to complete your first booking.";
                    return RedirectToPage("/Account/VerifyPhone", new { returnUrl });
                }
            }
        }

        // Get quantity and rental hours from form or TempData (if returning from OTP verification)
        int bookingQuantity = 1; // Default
        decimal bookingRentalHours = 2; // Default
        DateTime startDate = DateTime.Now; // Always start immediately
        DateTime endDate = DateTime.Now;

        // First, try to get from TempData (if returning from OTP verification)
        if (TempData.ContainsKey("BookingQuantity") && int.TryParse(TempData["BookingQuantity"]?.ToString(), out var tempQty))
        {
            bookingQuantity = tempQty;
            TempData.Remove("BookingQuantity");
        }
        else if (Request.Form.ContainsKey("quantity") && int.TryParse(Request.Form["quantity"], out var qty))
        {
            bookingQuantity = qty;
        }

        if (bookingQuantity < 1 || bookingQuantity > 10)
        {
            TempData["ErrorMessage"] = "Quantity must be between 1 and 10 bikes";
            return Page();
        }

        // Get hours from TempData or form
        if (TempData.ContainsKey("BookingHours") && decimal.TryParse(TempData["BookingHours"]?.ToString(), out var tempHours))
        {
            bookingRentalHours = tempHours;
            TempData.Remove("BookingHours");
            endDate = startDate.AddHours((double)bookingRentalHours);
        }
        else if (Request.Form.ContainsKey("rentalHours") && decimal.TryParse(Request.Form["rentalHours"], out var hours))
        {
            bookingRentalHours = hours;
            if (bookingRentalHours < 1 || bookingRentalHours > 168)
            {
                TempData["ErrorMessage"] = "Rental duration must be between 1 and 168 hours (7 days)";
                return Page();
            }
            endDate = startDate.AddHours((double)bookingRentalHours);
        }
        else if (Input.StartDate.HasValue && Input.EndDate.HasValue)
        {
            // Fallback to date inputs if provided (for backward compatibility)
            startDate = Input.StartDate.Value;
            endDate = Input.EndDate.Value;
            
            // Ensure start date is current time (booking starts immediately)
            if (startDate > DateTime.Now.AddMinutes(5))
            {
                TempData["ErrorMessage"] = "Booking must start immediately. Future bookings are not allowed.";
                return Page();
            }
            
            startDate = DateTime.Now; // Force to current time
            var duration = endDate - Input.StartDate.Value;
            endDate = startDate.Add(duration);
            bookingRentalHours = (decimal)duration.TotalHours;
        }
        else
        {
            TempData["ErrorMessage"] = "Please enter the rental duration in hours";
            return Page();
        }

        // Validate that end date is after start date
        if (endDate <= startDate)
        {
            TempData["ErrorMessage"] = "Invalid rental duration";
            return Page();
        }

        // Check bike availability for quantity
        if (Bike == null)
        {
            TempData["ErrorMessage"] = "Bike not found.";
            return Page();
        }

        // Check if requested quantity is available
        if (bookingQuantity > Bike.Quantity)
        {
            TempData["ErrorMessage"] = $"This listing only has {Bike.Quantity} bike(s) available. Please reduce the quantity.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadBikeDataAsync(id);
            return Page();
        }

        // Create a single booking with quantity
        var result = await _bookingService.CreateBookingAsync(
            userId.Value,
            id,
            startDate,
            endDate,
            bookingQuantity
        );

        if (result.success)
        {
            return RedirectToPage("/Bookings/Payment", new { bookingId = result.bookingId });
        }
        else
        {
            TempData["ErrorMessage"] = result.message;
            await LoadBikeDataAsync(id);
            return Page();
        }
    }
}

