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
    public int TotalRentals { get; set; }
    
    // Owner Specific
    public List<Bike> MyBikes { get; set; } = new();
    public List<Booking> PendingRequests { get; set; } = new();
    public int TotalEarnings { get; set; }
    public int ActiveListings { get; set; }
    
    // Shared Stats
    public decimal WalletBalance { get; set; }
    public int PointsBalance { get; set; }
    public int UnreadNotifications { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        
        CurrentUser = await _context.Users
            .Include(u => u.Wallet)
            .Include(u => u.Points)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (CurrentUser == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Get wallet and points balance
        WalletBalance = CurrentUser.Wallet?.Balance ?? 0;
                   PointsBalance = CurrentUser.Points?.TotalPoints ?? 0;

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
            .Where(b => b.AvailabilityStatusId == 1 && !excludeBikeIds.Contains(b.BikeId))
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
            ActiveRentals = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(b => b.BikeImages)
                .Include(b => b.BookingStatus)
                .Where(b => b.RenterId == userId && 
                           (b.BookingStatus.StatusName == "Confirmed" || b.BookingStatus.StatusName == "Active"))
                .OrderByDescending(b => b.StartDate)
                .Take(3)
                .ToListAsync();

            RecentRentals = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(b => b.BikeImages)
                .Include(b => b.BookingStatus)
                .Where(b => b.RenterId == userId && b.BookingStatus.StatusName == "Completed")
                .OrderByDescending(b => b.EndDate)
                .Take(3)
                .ToListAsync();

            TotalRentals = await _context.Bookings
                .Where(b => b.RenterId == userId)
                .CountAsync();
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
                .Include(b => b.BookingStatus)
                .Where(b => b.Bike.OwnerId == userId && b.BookingStatus.StatusName == "Pending")
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToListAsync();

            var completedBookings = await _context.Bookings
                .Include(b => b.Bike)
                .Where(b => b.Bike.OwnerId == userId && b.BookingStatus.StatusName == "Completed")
                .ToListAsync();
            
            TotalEarnings = (int)completedBookings.Sum(b => b.TotalAmount);

            ActiveListings = await _context.Bikes
                .Where(b => b.OwnerId == userId && b.AvailabilityStatusId == 1)
                .CountAsync();
        }

        return Page();
    }
}

