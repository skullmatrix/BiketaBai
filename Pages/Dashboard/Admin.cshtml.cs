using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Dashboard
{
    [Authorize(Roles = "Admin")]
    public class AdminModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public AdminModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        // System-wide Statistics
        public int TotalUsers { get; set; }
        public int TotalRenters { get; set; }
        public int TotalOwners { get; set; }
        public int TotalBikes { get; set; }
        public int TotalActiveRentals { get; set; }
        public int TotalCompletedRentals { get; set; }
        public decimal TotalPlatformRevenue { get; set; }
        public int PendingOwnerVerifications { get; set; }
        public int FlaggedListings { get; set; }
        public int SuspendedUsers { get; set; }

        // Recent Activity
        public List<User> RecentUsers { get; set; } = new List<User>();
        public List<Bike> RecentBikes { get; set; } = new List<Bike>();
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();
        public List<User> PendingVerifications { get; set; } = new List<User>();

        // Charts data
        public Dictionary<string, int> UsersByMonth { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> BookingsByMonth { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, decimal> RevenueByMonth { get; set; } = new Dictionary<string, decimal>();

        public async Task<IActionResult> OnGetAsync()
        {
            // Get all users
            var allUsers = await _context.Users.ToListAsync();
            TotalUsers = allUsers.Count;
            TotalRenters = allUsers.Count(u => u.IsRenter);
            TotalOwners = allUsers.Count(u => u.IsOwner);
            SuspendedUsers = allUsers.Count(u => u.IsSuspended);

            // Get all bikes
            var allBikes = await _context.Bikes.Include(b => b.Owner).ToListAsync();
            TotalBikes = allBikes.Count;

            // Get pending owner verifications
            PendingOwnerVerifications = allUsers.Count(u => u.IsOwner && u.VerificationStatus == "Pending");
            PendingVerifications = allUsers
                .Where(u => u.IsOwner && u.VerificationStatus == "Pending")
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToList();

            // Get all bookings
            var allBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .ToListAsync();

            TotalActiveRentals = allBookings.Count(b => 
                b.BookingStatus.StatusName == "Active" || 
                b.BookingStatus.StatusName == "Confirmed");
            
            TotalCompletedRentals = allBookings.Count(b => 
                b.BookingStatus.StatusName == "Completed");

            // Calculate platform revenue (10% service fee from completed bookings)
            TotalPlatformRevenue = allBookings
                .Where(b => b.BookingStatus.StatusName == "Completed")
                .Sum(b => b.TotalAmount * 0.10m);

            // Get recent activity
            RecentUsers = allUsers
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToList();

            RecentBikes = allBikes
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToList();

            RecentBookings = allBookings
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .ToList();

            // Prepare data for charts (last 6 months)
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);

            var usersByMonth = allUsers
                .Where(u => u.CreatedAt >= sixMonthsAgo)
                .GroupBy(u => u.CreatedAt.ToString("MMM yyyy"))
                .ToDictionary(g => g.Key, g => g.Count());

            var bookingsByMonth = allBookings
                .Where(b => b.CreatedAt >= sixMonthsAgo)
                .GroupBy(b => b.CreatedAt.ToString("MMM yyyy"))
                .ToDictionary(g => g.Key, g => g.Count());

            var revenueByMonth = allBookings
                .Where(b => b.CreatedAt >= sixMonthsAgo && b.BookingStatus.StatusName == "Completed")
                .GroupBy(b => b.CreatedAt.ToString("MMM yyyy"))
                .ToDictionary(g => g.Key, g => g.Sum(b => b.TotalAmount * 0.10m));

            UsersByMonth = usersByMonth;
            BookingsByMonth = bookingsByMonth;
            RevenueByMonth = revenueByMonth;

            return Page();
        }

        public string GetRoleBadge(User user)
        {
            var badges = new List<string>();
            
            if (user.IsAdmin) badges.Add("<span class='badge bg-danger'>Admin</span>");
            if (user.IsOwner) badges.Add("<span class='badge bg-warning'>Owner</span>");
            if (user.IsRenter) badges.Add("<span class='badge bg-info'>Renter</span>");
            
            return string.Join(" ", badges);
        }
    }
}

