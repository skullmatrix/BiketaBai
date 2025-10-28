using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Owner
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ProfileModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public User CurrentOwner { get; set; } = null!;
        
        // Statistics
        public int TotalBikes { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalEarnings { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Wallet & Payout
        public BiketaBai.Models.Wallet? UserWallet { get; set; }
        public decimal PendingPayout { get; set; }
        public decimal LifetimeEarnings { get; set; }

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

            CurrentOwner = await _context.Users.FindAsync(userId);
            if (CurrentOwner == null || !CurrentOwner.IsOwner)
            {
                return RedirectToPage("/AccessDenied");
            }

            // Load bikes
            var bikes = await _context.Bikes
                .Include(b => b.BikeImages)
                .Where(b => b.OwnerId == userId)
                .ToListAsync();
            TotalBikes = bikes.Count;

            // Load bookings for owner's bikes
            var allBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeImages)
                .Include(b => b.Renter)
                .Where(b => b.Bike.OwnerId == userId)
                .ToListAsync();

            TotalBookings = allBookings.Count;
            CompletedBookings = allBookings.Count(b => b.BookingStatus.StatusName == "Completed");
            
            // Calculate earnings (90% to owner, 10% platform fee)
            TotalEarnings = allBookings
                .Where(b => b.BookingStatus.StatusName == "Completed" || b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount * 0.90m);
            
            LifetimeEarnings = TotalEarnings;
            
            // Calculate pending payout (from active bookings)
            PendingPayout = allBookings
                .Where(b => b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount * 0.90m);

            // Load wallet
            UserWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);

            // Load ratings (renters rating this owner)
            var ratingsReceived = await _context.Ratings
                .Where(r => r.RatedUserId == userId && r.IsRenterRatingOwner)
                .ToListAsync();
            
            TotalReviews = ratingsReceived.Count;
            AverageRating = ratingsReceived.Any() ? ratingsReceived.Average(r => r.RatingValue) : 0;

            // Load recent bookings
            RecentBookings = allBookings
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToList();

            // Populate input model
            Input.FullName = CurrentOwner.FullName;
            Input.Phone = CurrentOwner.Phone;
            Input.Address = CurrentOwner.Address;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            CurrentOwner = await _context.Users.FindAsync(userId);
            if (CurrentOwner == null)
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

            // Update owner profile
            CurrentOwner.FullName = Input.FullName.Trim();
            CurrentOwner.Phone = Input.Phone.Trim();
            CurrentOwner.Address = Input.Address.Trim();
            CurrentOwner.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = "Profile updated successfully!";
            return RedirectToPage();
        }

        public string GetVerificationStatusBadge()
        {
            return CurrentOwner.VerificationStatus switch
            {
                "Approved" => "<span class='badge bg-success'><i class='bi bi-patch-check-fill'></i> Verified Owner</span>",
                "Pending" => "<span class='badge bg-warning'><i class='bi bi-hourglass-split'></i> Verification Pending</span>",
                "Rejected" => "<span class='badge bg-danger'><i class='bi bi-x-circle-fill'></i> Verification Denied</span>",
                _ => "<span class='badge bg-secondary'><i class='bi bi-question-circle'></i> Not Verified</span>"
            };
        }
    }
}

