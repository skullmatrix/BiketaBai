using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Dashboard;

[Authorize]
public class HomeModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public HomeModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    // User Info
    public User CurrentUser { get; set; } = null!;
    
    // Suggested Bikes
    public List<Bike> SuggestedBikes { get; set; } = new();
    public Dictionary<int, double> BikeRatings { get; set; } = new();
    
    // Renter Specific
    public List<Booking> ActiveRentals { get; set; } = new();
    public List<Booking> RecentRentals { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
    public int TotalRentals { get; set; }
    public int ActiveRentalsCount { get; set; }
    public int TotalRentalsCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal CO2Saved { get; set; }
    
    // Owner Specific
    public List<Bike> MyBikes { get; set; } = new();
    public List<Booking> PendingRequests { get; set; } = new();
    public int TotalEarnings { get; set; }
    public int ActiveListings { get; set; }
    
    // Shared Stats
    public int UnreadNotifications { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Redirect admins to admin dashboard
        if (AuthHelper.IsAdmin(User))
        {
            return RedirectToPage("/Admin/Dashboard");
        }

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        
        CurrentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (CurrentUser == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Get unread notifications count
        UnreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync();

        // Get suggested bikes (excluding user's own bikes if owner)
        var excludeBikeIds = new List<int>();
        if (AuthHelper.IsOwner(User))
        {
            excludeBikeIds = await _context.Bikes
                .Where(b => b.OwnerId == userId)
                .Select(b => b.BikeId)
                .ToListAsync();
        }

        SuggestedBikes = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => b.AvailabilityStatus == "Available" && !excludeBikeIds.Contains(b.BikeId))
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .ToListAsync();

        // Calculate ratings for suggested bikes
        foreach (var bike in SuggestedBikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();
            
            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

        // Renter specific data
        if (AuthHelper.IsRenter(User))
        {
            // Get active bookings (status 2 = Active)
            ActiveRentals = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(b => b.BikeImages)
                .Include(b => b.Bike)
                    .ThenInclude(b => b.Owner)
                .Include(b => b.Bike)
                    .ThenInclude(b => b.BikeType)
                .Where(b => b.RenterId == userId && b.BookingStatus == "Active")
                .OrderByDescending(b => b.StartDate)
                .ToListAsync();

            ActiveRentalsCount = ActiveRentals.Count;

            // Get recent bookings (all statuses, latest first)
            RecentBookings = await _context.Bookings
                .Include(b => b.Bike)
                .Where(b => b.RenterId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .ToListAsync();

            // Get completed rentals for stats
            RecentRentals = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(b => b.BikeImages)
                .Where(b => b.RenterId == userId && b.BookingStatus == "Completed")
                .OrderByDescending(b => b.EndDate)
                .Take(3)
                .ToListAsync();

            TotalRentals = await _context.Bookings
                .Where(b => b.RenterId == userId)
                .CountAsync();
            
            TotalRentalsCount = await _context.Bookings
                .Where(b => b.RenterId == userId && b.BookingStatus == "Completed")
                .CountAsync();
            
            TotalSpent = await _context.Bookings
                .Where(b => b.RenterId == userId && b.BookingStatus == "Completed")
                .SumAsync(b => b.TotalAmount);

            var totalKmSaved = await _context.Bookings
                .Where(b => b.RenterId == userId && b.DistanceSavedKm.HasValue)
                .SumAsync(b => b.DistanceSavedKm ?? 0);
            CO2Saved = totalKmSaved * 0.2m;
        }

        // Owner specific data
        if (AuthHelper.IsOwner(User))
        {
            MyBikes = await _context.Bikes
                .Include(b => b.BikeType)
                .Include(b => b.BikeImages)
                .Where(b => b.OwnerId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Take(4)
                .ToListAsync();

            PendingRequests = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .Where(b => b.Bike.OwnerId == userId && b.BookingStatus == "Pending")
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            var completedBookings = await _context.Bookings
                .Include(b => b.Bike)
                .Where(b => b.Bike.OwnerId == userId && b.BookingStatus == "Completed")
                .ToListAsync();
            
            TotalEarnings = (int)completedBookings.Sum(b => b.TotalAmount);

            ActiveListings = await _context.Bikes
                .Where(b => b.OwnerId == userId && b.AvailabilityStatus == "Available")
                .CountAsync();
        }

        return Page();
    }
}

