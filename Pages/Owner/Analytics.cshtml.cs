using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using System;
using System.Linq;

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
    public decimal TotalServiceFees { get; set; }
    public decimal DailyServiceFees { get; set; }
    public decimal WeeklyServiceFees { get; set; }
    public decimal MonthlyServiceFees { get; set; }
    public decimal TotalRevenue { get; set; } // Total before service fee deduction
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
        try
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

        // Calculate total revenue (before service fee)
        TotalRevenue = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && b.BookingStatus == "Completed")
            .SumAsync(b => b.TotalAmount);

        // Calculate earnings (90% after service fee)
        TotalEarnings = TotalRevenue * 0.9m;
        
        // Calculate service fees (10%)
        TotalServiceFees = TotalRevenue * 0.1m;

        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dailyRevenue = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= todayStart)
            .SumAsync(b => b.TotalAmount);
        DailyEarnings = dailyRevenue * 0.9m;
        DailyServiceFees = dailyRevenue * 0.1m;

        var weeklyRevenue = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= weekStart)
            .SumAsync(b => b.TotalAmount);
        WeeklyEarnings = weeklyRevenue * 0.9m;
        WeeklyServiceFees = weeklyRevenue * 0.1m;

        var monthlyRevenue = await _context.Bookings
            .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                       b.BookingStatus == "Completed" &&
                       b.UpdatedAt >= monthStart)
            .SumAsync(b => b.TotalAmount);
        MonthlyEarnings = monthlyRevenue * 0.9m;
        MonthlyServiceFees = monthlyRevenue * 0.1m;

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

        // Total bikes should be sum of quantities, not count of listings
        TotalBikes = await _context.Bikes
            .Where(b => b.OwnerId == userId.Value && !b.IsDeleted)
            .SumAsync(b => b.Quantity);

        // Average rating
        var ownerRatings = await _context.Ratings
            .Where(r => r.RatedUserId == userId.Value && r.IsRenterRatingOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();
        AverageRating = ownerRatings.Any() ? ownerRatings.Average() : 0;

        // Daily Earnings Chart (last 30 days)
        try
        {
            var thirtyDaysAgo = now.AddDays(-30);
            var completedBookings = await _context.Bookings
                .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                           b.BookingStatus == "Completed")
                .ToListAsync();

            // Group by date - use ActualReturnDate if available, otherwise EndDate, otherwise UpdatedAt
            var dailyEarningsData = completedBookings
                .Select(b => new { 
                    Booking = b, 
                    DateToUse = b.ActualReturnDate ?? (DateTime?)b.EndDate ?? (DateTime?)b.UpdatedAt 
                })
                .Where(b => b.DateToUse.HasValue && b.DateToUse.Value >= thirtyDaysAgo)
                .GroupBy(b => b.DateToUse!.Value.Date)
                .Select(g => new DailyEarningsData
                {
                    Date = g.Key.ToString("MMM dd, yyyy"),
                    Amount = g.Sum(b => b.Booking.TotalAmount * 0.9m)
                })
                .OrderBy(e => e.Date)
                .ToList();

            // Fill in missing dates with zero earnings for better chart visualization
            var allDates = Enumerable.Range(0, 30)
                .Select(i => now.AddDays(-i).Date)
                .OrderBy(d => d)
                .ToList();

            var existingDates = dailyEarningsData.Select(d => DateTime.ParseExact(d.Date, "MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture).Date).ToHashSet();
            
            foreach (var date in allDates)
            {
                if (!existingDates.Contains(date))
                {
                    dailyEarningsData.Add(new DailyEarningsData
                    {
                        Date = date.ToString("MMM dd, yyyy"),
                        Amount = 0
                    });
                }
            }

            DailyEarningsChart = dailyEarningsData.OrderBy(e => e.Date).ToList();
        }
        catch (Exception ex)
        {
            DailyEarningsChart = new List<DailyEarningsData>();
        }

        // Weekly Earnings Chart (last 12 weeks)
        try
        {
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
        }
        catch
        {
            WeeklyEarningsChart = new List<WeeklyEarningsData>();
        }

        // Monthly Earnings Chart (last 12 months)
        try
        {
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
            MonthlyEarningsChart = monthlyEarningsData ?? new List<MonthlyEarningsData>();
        }
        catch
        {
            MonthlyEarningsChart = new List<MonthlyEarningsData>();
        }

        // Bike Type Earnings
        try
        {
            var bikeTypeEarnings = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeType)
                .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                           b.BookingStatus == "Completed" &&
                           b.UpdatedAt >= startDate &&
                           b.Bike != null &&
                           b.Bike.BikeType != null)
                .GroupBy(b => b.Bike.BikeType.TypeName)
                .Select(g => new BikeTypeEarningsData
                {
                    TypeName = g.Key ?? "Unknown",
                    Amount = g.Sum(b => b.TotalAmount * 0.9m),
                    Count = g.Count()
                })
                .OrderByDescending(b => b.Amount)
                .ToListAsync();
            BikeTypeEarningsChart = bikeTypeEarnings ?? new List<BikeTypeEarningsData>();
        }
        catch
        {
            BikeTypeEarningsChart = new List<BikeTypeEarningsData>();
        }

        // Booking Status Chart
        try
        {
            var bookingStatusData = await _context.Bookings
                .Where(b => ownerBikeIds.Contains(b.BikeId) && !string.IsNullOrEmpty(b.BookingStatus))
                .GroupBy(b => b.BookingStatus)
                .Select(g => new BookingStatusData
                {
                    Status = g.Key ?? "Unknown",
                    Count = g.Count()
                })
                .ToListAsync();
            BookingStatusChart = bookingStatusData ?? new List<BookingStatusData>();
        }
        catch
        {
            BookingStatusChart = new List<BookingStatusData>();
        }

        // Top Performing Bikes
        try
        {
            var topBikes = await _context.Bookings
                .Include(b => b.Bike)
                .Where(b => ownerBikeIds.Contains(b.BikeId) && 
                           b.BookingStatus == "Completed" &&
                           b.UpdatedAt >= startDate &&
                           b.Bike != null)
                .GroupBy(b => new { b.BikeId, b.Bike.Brand, b.Bike.Model })
                .Select(g => new TopBikeData
                {
                    BikeName = $"{g.Key.Brand ?? "Unknown"} {g.Key.Model ?? ""}".Trim(),
                    Earnings = g.Sum(b => b.TotalAmount * 0.9m),
                    Bookings = g.Count()
                })
                .OrderByDescending(b => b.Earnings)
                .Take(10)
                .ToListAsync();
            TopBikesChart = topBikes ?? new List<TopBikeData>();
        }
        catch
        {
            TopBikesChart = new List<TopBikeData>();
        }

            return Page();
        }
        catch (Exception ex)
        {
            // Log error (you can add logging here)
            // For now, return page with empty data to prevent crash
            return Page();
        }
    }

    private int GetWeekOfYear(DateTime date)
    {
        try
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var calendar = culture.Calendar;
            return calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }
        catch
        {
            // Fallback calculation if culture-specific method fails
            var jan1 = new DateTime(date.Year, 1, 1);
            var daysOffset = (int)jan1.DayOfWeek;
            var firstWeekDay = jan1.AddDays(-daysOffset);
            var weekNum = ((date - firstWeekDay).Days / 7) + 1;
            return weekNum;
        }
    }
}

