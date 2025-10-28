using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Renter
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ProfileModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public User CurrentUser { get; set; } = null!;
        
        // Statistics
        public int TotalRentals { get; set; }
        public int CompletedRentals { get; set; }
        public int ActiveRentals { get; set; }
        public decimal TotalSpent { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Wallet & Points
        public BiketaBai.Models.Wallet? UserWallet { get; set; }
        public BiketaBai.Models.Points? UserPoints { get; set; }

        // Recent Activity
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            CurrentUser = await _context.Users.FindAsync(userId);
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Load statistics
            var allBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeImages)
                .Include(b => b.Bike.BikeType)
                .Where(b => b.RenterId == userId)
                .ToListAsync();

            TotalRentals = allBookings.Count;
            CompletedRentals = allBookings.Count(b => b.BookingStatus.StatusName == "Completed");
            ActiveRentals = allBookings.Count(b => 
                b.BookingStatus.StatusName == "Active" || 
                b.BookingStatus.StatusName == "Confirmed");
            TotalSpent = allBookings
                .Where(b => b.BookingStatus.StatusName == "Completed" || b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount);

            // Load wallet and points
            UserWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            UserPoints = await _context.Points.FirstOrDefaultAsync(p => p.UserId == userId);

            // Load ratings given by others to this renter (owner rating renter)
            var ratingsReceived = await _context.Ratings
                .Where(r => r.RatedUserId == userId && !r.IsRenterRatingOwner)
                .ToListAsync();
            
            TotalReviews = ratingsReceived.Count;
            AverageRating = ratingsReceived.Any() ? ratingsReceived.Average(r => r.RatingValue) : 0;

            // Load recent bookings
            RecentBookings = allBookings
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToList();

            // Populate input model
            Input.FullName = CurrentUser.FullName;
            Input.Phone = CurrentUser.Phone;
            Input.Address = CurrentUser.Address;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            CurrentUser = await _context.Users.FindAsync(userId);
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            if (string.IsNullOrWhiteSpace(Input.FullName) || 
                string.IsNullOrWhiteSpace(Input.Phone) || 
                string.IsNullOrWhiteSpace(Input.Address))
            {
                ErrorMessage = "All fields are required";
                return await OnGetAsync();
            }

            // Update user profile
            CurrentUser.FullName = Input.FullName.Trim();
            CurrentUser.Phone = Input.Phone.Trim();
            CurrentUser.Address = Input.Address.Trim();
            CurrentUser.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = "Profile updated successfully!";
            return RedirectToPage();
        }
    }
}

