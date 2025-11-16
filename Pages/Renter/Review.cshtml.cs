using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Renter
{
    [Authorize]
    public class ReviewModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;
        private readonly RatingService _ratingService;

        public ReviewModel(BiketaBaiDbContext context, RatingService ratingService)
        {
            _context = context;
            _ratingService = ratingService;
        }

        public Booking? Booking { get; set; }
        public bool HasRated { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Please select a rating")]
            [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
            public int RatingValue { get; set; }

            [Display(Name = "Review (Optional)")]
            [MaxLength(1000, ErrorMessage = "Review cannot exceed 1000 characters")]
            public string? Review { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int bookingId)
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            Booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeImages)
                .Include(b => b.Bike.BikeType)
                .Include(b => b.Bike.Owner)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

            if (Booking == null)
                return NotFound();

            if (Booking.BookingStatus.StatusName != "Completed")
            {
                TempData["ErrorMessage"] = "You can only review completed bookings.";
                return RedirectToPage("/Renter/RentalHistory");
            }

            HasRated = await _ratingService.HasRatedBookingAsync(bookingId, userId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int bookingId)
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            Booking = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Bike.Owner)
                .Include(b => b.BookingStatus)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

            if (Booking == null)
                return NotFound();

            if (Booking.BookingStatus.StatusName != "Completed")
            {
                TempData["ErrorMessage"] = "You can only review completed bookings.";
                return RedirectToPage("/Renter/RentalHistory");
            }

            if (await _ratingService.HasRatedBookingAsync(bookingId, userId.Value))
            {
                TempData["ErrorMessage"] = "You have already reviewed this booking.";
                return RedirectToPage("/Renter/RentalHistory");
            }

            if (!ModelState.IsValid)
            {
                HasRated = false;
                return Page();
            }

            // Submit rating for the bike (renter rating the bike and owner)
            // This rating will appear on both the bike details page and count towards owner rating
            var success = await _ratingService.SubmitRatingAsync(
                bookingId: bookingId,
                raterId: userId.Value,
                ratedUserId: Booking.Bike.OwnerId,
                bikeId: Booking.BikeId,
                ratingValue: Input.RatingValue,
                review: Input.Review,
                isRenterRatingOwner: true // true means renter is rating the owner (via bike rental)
            );

            if (success)
            {
                TempData["SuccessMessage"] = "Thank you! Your review has been submitted.";
                return RedirectToPage("/Renter/RentalHistory");
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to submit review. Please try again.";
                return Page();
            }
        }
    }
}

