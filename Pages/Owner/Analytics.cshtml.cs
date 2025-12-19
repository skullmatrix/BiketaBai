using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class AnalyticsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public AnalyticsModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public decimal TotalEarnings { get; set; }
    public decimal DailyEarnings { get; set; }
    public decimal WeeklyEarnings { get; set; }
    public decimal MonthlyEarnings { get; set; }
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int TotalBikes { get; set; }
    public double AverageRating { get; set; }

    // Chart Data
    public List<DailyEarningsData> DailyEarningsChart { get; set; } = new();
    public List<WeeklyEarningsData> WeeklyEarningsChart { get; set; } = new();
    public List<MonthlyEarningsData> MonthlyEarningsChart { get; set; } = new();
    public List<BikeTypeEarningsData> BikeTypeEarningsChart { get; set; } = new();
    public List<BookingStatusData> BookingStatusChart { get; set; } = new();
    public List<TopBikeData> TopBikesChart { get; set; } = new();

    public class DailyEarningsData
    {
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class WeeklyEarningsData
    {
        public string Week { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class MonthlyEarningsData
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BikeTypeEarningsData
    {
        public string TypeName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class BookingStatusData
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TopBikeData
    {
        public string BikeName { get; set; } = string.Empty;
        public decimal Earnings { get; set; }
        public int Bookings { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int days = 30)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Get owner's bike IDs
        var ownerBikeIds = await _context.Bikes
            .Where(b => b.OwnerId == userId.Value)
            .Select(b => b.BikeId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-days);

        // Calculate earnings
        TotalEarnings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Completed")
            .SumAsync(b => b.TotalAmount * 0.9m);

        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

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

        // Calculate booking stats
        TotalBookings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId))
            .CountAsync();

        CompletedBookings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Completed")
            .CountAsync();

        ActiveBookings = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Active")
            .CountAsync();

        TotalBikes = await _context.Bikes
            .Where(b => b.OwnerId == userId.Value)
            .CountAsync();

        // Average rating
        var ownerRatings = await _context.Ratings
            .Where(r => r.RatedUserId == userId.Value && r.IsRenterRatingOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();
        AverageRating = ownerRatings.Any() ? ownerRatings.Average() : 0;

        // Daily Earnings Chart (last 30 days)
        var dailyEarningsData = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= now.AddDays(-30))
            .GroupBy(b => b.UpdatedAt.Date)
            .Select(g => new DailyEarningsData
            {
                Date = g.Key.ToString("MMM dd"),
                Amount = g.Sum(b => b.TotalAmount * 0.9m)
            })
            .OrderBy(e => e.Date)
            .ToListAsync();
        DailyEarningsChart = dailyEarningsData;

        // Weekly Earnings Chart (last 12 weeks)
        var weeklyEarningsData = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= now.AddDays(-84))
            .ToListAsync();

        WeeklyEarningsChart = weeklyEarningsData
            .GroupBy(b => new { 
                Year = b.UpdatedAt.Year, 
                Week = GetWeekOfYear(b.UpdatedAt) 
            })
            .Select(g => new WeeklyEarningsData
            {
                Week = $"Week {g.Key.Week}, {g.Key.Year}",
                Amount = g.Sum(b => b.TotalAmount * 0.9m)
            })
            .OrderBy(w => w.Week)
            .Take(12)
            .ToList();

        // Monthly Earnings Chart (last 12 months)
        var monthlyEarningsData = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= now.AddMonths(-12))
            .GroupBy(b => new { b.UpdatedAt.Year, b.UpdatedAt.Month })
            .Select(g => new MonthlyEarningsData
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Amount = g.Sum(b => b.TotalAmount * 0.9m)
            })
            .OrderBy(m => m.Month)
            .ToListAsync();
        MonthlyEarningsChart = monthlyEarningsData;

        // Bike Type Earnings
        var bikeTypeEarnings = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= startDate)
            .GroupBy(b => b.Bike.BikeType.TypeName)
            .Select(g => new BikeTypeEarningsData
            {
                TypeName = g.Key,
                Amount = g.Sum(b => b.TotalAmount * 0.9m),
                Count = g.Count()
            })
            .OrderByDescending(b => b.Amount)
            .ToListAsync();
        BikeTypeEarningsChart = bikeTypeEarnings;

        // Booking Status Chart
        var bookingStatusData = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId))
            .GroupBy(b => b.BookingStatus)
            .Select(g => new BookingStatusData
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();
        BookingStatusChart = bookingStatusData;

        // Top Performing Bikes
        var topBikes = await _context.Bookings
            .Include(b => b.Bike)
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= startDate)
            .GroupBy(b => new { b.BikeId, b.Bike.Brand, b.Bike.Model })
            .Select(g => new TopBikeData
            {
                BikeName = $"{g.Key.Brand} {g.Key.Model}",
                Earnings = g.Sum(b => b.TotalAmount * 0.9m),
                Bookings = g.Count()
            })
            .OrderByDescending(b => b.Earnings)
            .Take(10)
            .ToListAsync();
        TopBikesChart = topBikes;

        return Page();
    }

    private int GetWeekOfYear(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var calendar = culture.Calendar;
        return calendar.GetWeekOfYear(date, culture.DateTimeFormat.CalendarWeekRule, culture.DateTimeFormat.FirstDayOfWeek);
    }
}

