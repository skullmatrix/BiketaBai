using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Dashboard;

public class OwnerDashboardModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BookingService _bookingService;

    public OwnerDashboardModel(BiketaBaiDbContext context, BookingService bookingService)
    {
        _context = context;
        _bookingService = bookingService;
    }

    public List<Bike> Bikes { get; set; } = new();
    public List<Booking> ActiveBookings { get; set; } = new();
    public List<Booking> PendingBookings { get; set; } = new();
    public List<Booking> OverdueBookings { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
    public Dictionary<int, double> BikeRatings { get; set; } = new();
    public int TotalBikes { get; set; }
    public int ActiveRentalsCount { get; set; }
    public int PendingRequestsCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal PendingEarnings { get; set; }
    public double AverageRating { get; set; }
    public int UnreadNotifications { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Get bikes
        Bikes = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Where(b => b.OwnerId == userId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Get owner's bike IDs
        var ownerBikeIds = await _context.Bikes
            .Where(b => b.OwnerId == userId.Value)
            .Select(b => b.BikeId)
            .ToListAsync();

        // Get active bookings
        ActiveBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Active")
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Get pending bookings (need approval)
        PendingBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Pending")
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Get overdue bookings
        var now = DateTime.UtcNow;
        OverdueBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Active" &&
                       b.EndDate < now) // Overdue
            .OrderBy(b => b.EndDate)
            .ToListAsync();

        // Get recent bookings
        RecentBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Where(b => ownerBikeIds.Contains(b.BikeId))
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Calculate ratings for bikes
        foreach (var bike in Bikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();
            
            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

        // Calculate stats
        TotalBikes = Bikes.Count;
        ActiveRentalsCount = ActiveBookings.Count;
        PendingRequestsCount = PendingBookings.Count;
        OverdueCount = OverdueBookings.Count;
        
        TotalEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Completed")
            .SumAsync(b => b.TotalAmount * 0.9m); // 90% after service fee

        PendingEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Active")
            .SumAsync(b => b.TotalAmount * 0.9m);

        var ownerRatings = await _context.Ratings
            .Where(r => r.RatedUserId == userId.Value && r.IsRenterRatingOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();
        AverageRating = ownerRatings.Any() ? ownerRatings.Average() : 0;

        // Get unread notifications count
        var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
        UnreadNotifications = await notificationService.GetUnreadCountAsync(userId.Value);

        return Page();
    }
}

