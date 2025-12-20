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
    public decimal DailyEarnings { get; set; }
    public decimal WeeklyEarnings { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public Dictionary<string, int> BikeTypeCounts { get; set; } = new();
    public Dictionary<string, int> BikeTypeAvailableCounts { get; set; } = new();
    public double AverageRating { get; set; }
    public int UnreadNotifications { get; set; }
    public List<EarningsData> EarningsHistory { get; set; } = new();
    public Dictionary<int, bool> BookingFlaggedStatus { get; set; } = new();
    public Dictionary<int, int> AvailableQuantities { get; set; } = new(); // BikeId -> Available Quantity
    public int TotalAvailableListings { get; set; }
    public int TotalAvailableUnits { get; set; }
    
    public class EarningsData
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }

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
        
        // Calculate earnings (90% after service fee)
        // 'now' is already defined above for overdue bookings
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        
        TotalEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Completed")
            .SumAsync(b => b.TotalAmount * 0.9m);

        PendingEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Active")
            .SumAsync(b => b.TotalAmount * 0.9m);

        DailyEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= todayStart)
            .SumAsync(b => b.TotalAmount * 0.9m);

        WeeklyEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= weekStart)
            .SumAsync(b => b.TotalAmount * 0.9m);

        MonthlyEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= monthStart)
            .SumAsync(b => b.TotalAmount * 0.9m);

        // Get earnings history for last 30 days
        var thirtyDaysAgo = now.AddDays(-30);
        EarningsHistory = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= thirtyDaysAgo)
            .GroupBy(b => b.UpdatedAt.Date)
            .Select(g => new EarningsData
            {
                Date = g.Key,
                Amount = g.Sum(b => b.TotalAmount * 0.9m)
            })
            .OrderBy(e => e.Date)
            .ToListAsync();

        // Calculate bike type counts
        var bikesWithTypes = await _context.Bikes
            .Include(b => b.BikeType)
            .Where(b => b.OwnerId == userId.Value)
            .ToListAsync();

        BikeTypeCounts = bikesWithTypes
            .GroupBy(b => b.BikeType.TypeName)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate available quantities for each bike based on active bookings
        var allActiveBookings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       (b.BookingStatus == "Active" || b.BookingStatus == "Pending") &&
                       !b.IsReportedLost) // Exclude lost bikes from active count
            .ToListAsync();

        var lostBookings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.IsReportedLost)
            .ToListAsync();

        foreach (var bike in Bikes)
        {
            var bikeActiveBookings = allActiveBookings.Where(b => b.BikeId == bike.BikeId).ToList();
            var bikeLostBookings = lostBookings.Where(b => b.BikeId == bike.BikeId).ToList();
            
            var activeRentedQuantity = bikeActiveBookings.Sum(b => b.Quantity);
            var lostQuantity = bikeLostBookings.Sum(b => b.Quantity);
            var totalRentedQuantity = activeRentedQuantity + lostQuantity;
            
            AvailableQuantities[bike.BikeId] = Math.Max(0, bike.Quantity - totalRentedQuantity);
        }

        // Calculate total available listings (bikes with at least 1 available unit)
        TotalAvailableListings = AvailableQuantities.Count(kvp => kvp.Value > 0);
        
        // Calculate total available units
        TotalAvailableUnits = AvailableQuantities.Values.Sum();

        // Calculate bike type available counts based on actual available quantities
        BikeTypeAvailableCounts = Bikes
            .Where(b => AvailableQuantities.ContainsKey(b.BikeId) && AvailableQuantities[b.BikeId] > 0)
            .GroupBy(b => b.BikeType?.TypeName ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(b => AvailableQuantities[b.BikeId]));

        var ownerRatings = await _context.Ratings
            .Where(r => r.RatedUserId == userId.Value && r.IsRenterRatingOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();
        AverageRating = ownerRatings.Any() ? ownerRatings.Average() : 0;

        // Get unread notifications count
        var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
        UnreadNotifications = await notificationService.GetUnreadCountAsync(userId.Value);

        // Check which completed bookings have been flagged
        var renterFlagService = HttpContext.RequestServices.GetRequiredService<RenterFlagService>();
        var completedBookings = RecentBookings.Where(b => b.BookingStatus == "Completed").ToList();
        foreach (var booking in completedBookings)
        {
            BookingFlaggedStatus[booking.BookingId] = await renterFlagService.HasFlaggedBookingAsync(booking.BookingId, userId.Value);
        }

        return Page();
    }
}

