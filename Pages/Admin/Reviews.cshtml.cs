using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ReviewsModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ReviewsModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public List<Rating> AllRatings { get; set; } = new List<Rating>();
        public List<Rating> FilteredRatings { get; set; } = new List<Rating>();

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }

        // Statistics
        public int TotalReviews { get; set; }
        public int BikeReviews { get; set; }
        public int OwnerReviews { get; set; }
        public double AverageRating { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            AllRatings = await _context.Ratings
                .Include(r => r.Rater)
                .Include(r => r.RatedUser)
                .Include(r => r.Bike)
                    .ThenInclude(b => b.BikeType)
                .Include(r => r.Booking)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Calculate statistics
            TotalReviews = AllRatings.Count;
            BikeReviews = AllRatings.Count(r => r.BikeId.HasValue && !r.IsRenterRatingOwner);
            OwnerReviews = AllRatings.Count(r => r.IsRenterRatingOwner);
            AverageRating = AllRatings.Any() ? AllRatings.Average(r => r.RatingValue) : 0;

            // Apply filters
            FilteredRatings = AllRatings;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredRatings = FilteredRatings
                    .Where(r =>
                        (r.Rater?.FullName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) == true) ||
                        (r.RatedUser?.FullName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) == true) ||
                        (r.Bike != null && (r.Bike.Brand.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                          r.Bike.Model.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))) ||
                        (r.Review != null && r.Review.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Type filter
            if (!string.IsNullOrWhiteSpace(TypeFilter))
            {
                FilteredRatings = TypeFilter switch
                {
                    "Bike" => FilteredRatings.Where(r => r.BikeId.HasValue && !r.IsRenterRatingOwner).ToList(),
                    "Owner" => FilteredRatings.Where(r => r.IsRenterRatingOwner).ToList(),
                    _ => FilteredRatings
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteReviewAsync(int ratingId)
        {
            var rating = await _context.Ratings.FindAsync(ratingId);
            if (rating == null)
            {
                ErrorMessage = "Review not found";
                return RedirectToPage();
            }

            _context.Ratings.Remove(rating);
            await _context.SaveChangesAsync();

            SuccessMessage = "Review deleted successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostFlagReviewAsync(int ratingId)
        {
            var rating = await _context.Ratings.FindAsync(ratingId);
            if (rating == null)
            {
                ErrorMessage = "Review not found";
                return RedirectToPage();
            }

            rating.IsFlagged = !rating.IsFlagged;
            await _context.SaveChangesAsync();

            SuccessMessage = rating.IsFlagged ? "Review flagged" : "Review unflagged";
            return RedirectToPage();
        }
    }
}

