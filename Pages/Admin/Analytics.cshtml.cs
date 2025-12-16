using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AnalyticsModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public AnalyticsModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        // Overall Statistics
        public int TotalUsers { get; set; }
        public int TotalBikes { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCO2Saved { get; set; }

        // Time-based Statistics
        public List<DailyStat> DailyBookings { get; set; } = new();
        public List<DailyStat> DailyRevenue { get; set; } = new();
        public List<MonthlyStat> MonthlyBookings { get; set; } = new();
        public List<MonthlyStat> MonthlyRevenue { get; set; } = new();

        // Category Statistics
        public List<PaymentMethodStat> PaymentMethodStats { get; set; } = new();
        public List<BikeTypeStat> BikeTypeStats { get; set; } = new();
        public List<StatusStat> BookingStatusStats { get; set; } = new();

        // Growth Metrics
        public decimal UserGrowthRate { get; set; }
        public decimal BookingGrowthRate { get; set; }
        public decimal RevenueGrowthRate { get; set; }

        // Top Performers
        public List<TopBike> TopBikes { get; set; } = new();
        public List<TopOwner> TopOwners { get; set; } = new();

        // Date Range Filter
        [BindProperty(SupportsGet = true)]
        public int Days { get; set; } = 30; // Default to last 30 days

        public class DailyStat
        {
            public string Date { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Amount { get; set; }
        }

        public class MonthlyStat
        {
            public string Month { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Amount { get; set; }
        }

        public class PaymentMethodStat
        {
            public string PaymentMethod { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal Percentage { get; set; }
        }

        public class BikeTypeStat
        {
            public string TypeName { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Percentage { get; set; }
        }

        public class StatusStat
        {
            public string Status { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Percentage { get; set; }
        }

        public class TopBike
        {
            public int BikeId { get; set; }
            public string BikeName { get; set; } = string.Empty;
            public string OwnerName { get; set; } = string.Empty;
            public int BookingCount { get; set; }
            public decimal TotalEarnings { get; set; }
            public decimal AvgRating { get; set; }
        }

        public class TopOwner
        {
            public int OwnerId { get; set; }
            public string OwnerName { get; set; } = string.Empty;
            public int BikeCount { get; set; }
            public int BookingCount { get; set; }
            public decimal TotalEarnings { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!AuthHelper.IsAdmin(User))
                return RedirectToPage("/Account/AccessDenied");

            try
            {
                var startDate = DateTime.UtcNow.AddDays(-Days);
                var previousPeriodStart = DateTime.UtcNow.AddDays(-Days * 2);
                var previousPeriodEnd = DateTime.UtcNow.AddDays(-Days);

                // Overall Statistics
                TotalUsers = await _context.Users.CountAsync();
                TotalBikes = await _context.Bikes.CountAsync();
                TotalBookings = await _context.Bookings.CountAsync();
                
                TotalRevenue = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed")
                    .SumAsync(b => b.ServiceFee);

                var totalKmSaved = await _context.Bookings
                    .Where(b => b.DistanceSavedKm.HasValue)
                    .SumAsync(b => b.DistanceSavedKm ?? 0);
                TotalCO2Saved = totalKmSaved * 0.2m;

                // Daily Bookings (last N days)
                var dailyBookingsData = await _context.Bookings
                    .Where(b => b.CreatedAt >= startDate)
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new DailyStat
                    {
                        Date = g.Key.ToString("MMM dd"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.TotalAmount)
                    })
                    .OrderBy(d => d.Date)
                    .ToListAsync();

                DailyBookings = dailyBookingsData;

                // Daily Revenue (from completed bookings)
                var dailyRevenueData = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= startDate)
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new DailyStat
                    {
                        Date = g.Key.ToString("MMM dd"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.ServiceFee)
                    })
                    .OrderBy(d => d.Date)
                    .ToListAsync();

                DailyRevenue = dailyRevenueData;

                // Monthly Bookings (last 12 months)
                var monthlyBookingsData = await _context.Bookings
                    .Where(b => b.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
                    .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                    .Select(g => new MonthlyStat
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.TotalAmount)
                    })
                    .OrderBy(m => m.Month)
                    .ToListAsync();

                MonthlyBookings = monthlyBookingsData;

                // Monthly Revenue
                var monthlyRevenueData = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
                    .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                    .Select(g => new MonthlyStat
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.ServiceFee)
                    })
                    .OrderBy(m => m.Month)
                    .ToListAsync();

                MonthlyRevenue = monthlyRevenueData;

                // Payment Method Statistics
                var paymentStats = await _context.Payments
                    .Where(p => p.PaymentStatus == "Completed")
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new PaymentMethodStat
                    {
                        PaymentMethod = g.Key ?? "Unknown",
                        Count = g.Count(),
                        TotalAmount = g.Sum(p => p.Amount)
                    })
                    .ToListAsync();

                var totalPayments = paymentStats.Sum(p => p.Count);
                foreach (var stat in paymentStats)
                {
                    stat.Percentage = totalPayments > 0 ? (decimal)stat.Count / totalPayments * 100 : 0;
                }
                PaymentMethodStats = paymentStats.OrderByDescending(p => p.Count).ToList();

                // Bike Type Statistics
                var bikeTypeStats = await _context.Bikes
                    .Include(b => b.BikeType)
                    .GroupBy(b => b.BikeType.TypeName)
                    .Select(g => new BikeTypeStat
                    {
                        TypeName = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var totalBikesForType = bikeTypeStats.Sum(b => b.Count);
                foreach (var stat in bikeTypeStats)
                {
                    stat.Percentage = totalBikesForType > 0 ? (decimal)stat.Count / totalBikesForType * 100 : 0;
                }
                BikeTypeStats = bikeTypeStats.OrderByDescending(b => b.Count).ToList();

                // Booking Status Statistics
                var statusStats = await _context.Bookings
                    .GroupBy(b => b.BookingStatus)
                    .Select(g => new StatusStat
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var totalBookingsForStatus = statusStats.Sum(s => s.Count);
                foreach (var stat in statusStats)
                {
                    stat.Percentage = totalBookingsForStatus > 0 ? (decimal)stat.Count / totalBookingsForStatus * 100 : 0;
                }
                BookingStatusStats = statusStats.OrderByDescending(s => s.Count).ToList();

                // Growth Rates
                var currentPeriodUsers = await _context.Users
                    .Where(u => u.CreatedAt >= startDate)
                    .CountAsync();
                var previousPeriodUsers = await _context.Users
                    .Where(u => u.CreatedAt >= previousPeriodStart && u.CreatedAt < previousPeriodEnd)
                    .CountAsync();
                UserGrowthRate = previousPeriodUsers > 0 
                    ? ((decimal)(currentPeriodUsers - previousPeriodUsers) / previousPeriodUsers) * 100 
                    : (currentPeriodUsers > 0 ? 100 : 0);

                var currentPeriodBookings = await _context.Bookings
                    .Where(b => b.CreatedAt >= startDate)
                    .CountAsync();
                var previousPeriodBookings = await _context.Bookings
                    .Where(b => b.CreatedAt >= previousPeriodStart && b.CreatedAt < previousPeriodEnd)
                    .CountAsync();
                BookingGrowthRate = previousPeriodBookings > 0 
                    ? ((decimal)(currentPeriodBookings - previousPeriodBookings) / previousPeriodBookings) * 100 
                    : (currentPeriodBookings > 0 ? 100 : 0);

                var currentPeriodRevenue = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= startDate)
                    .SumAsync(b => b.ServiceFee);
                var previousPeriodRevenue = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= previousPeriodStart && b.CreatedAt < previousPeriodEnd)
                    .SumAsync(b => b.ServiceFee);
                RevenueGrowthRate = previousPeriodRevenue > 0 
                    ? ((currentPeriodRevenue - previousPeriodRevenue) / previousPeriodRevenue) * 100 
                    : (currentPeriodRevenue > 0 ? 100 : 0);

                // Top Bikes
                var topBikesData = await _context.Bookings
                    .Include(b => b.Bike)
                        .ThenInclude(bike => bike.Owner)
                    .Include(b => b.Ratings)
                    .Where(b => b.BookingStatus == "Completed")
                    .GroupBy(b => new { b.BikeId, b.Bike.Brand, b.Bike.Model, b.Bike.Owner.FullName })
                    .Select(g => new TopBike
                    {
                        BikeId = g.Key.BikeId,
                        BikeName = $"{g.Key.Brand} {g.Key.Model}",
                        OwnerName = g.Key.FullName,
                        BookingCount = g.Count(),
                        TotalEarnings = g.Sum(b => b.TotalAmount * 0.9m), // 90% after service fee
                        AvgRating = g.SelectMany(b => b.Ratings)
                            .Where(r => r.RatingValue > 0)
                            .DefaultIfEmpty()
                            .Average(r => r != null ? (decimal)r.RatingValue : 0)
                    })
                    .OrderByDescending(b => b.TotalEarnings)
                    .Take(10)
                    .ToListAsync();

                TopBikes = topBikesData;

                // Top Owners
                var topOwnersData = await _context.Bikes
                    .Include(b => b.Owner)
                    .Include(b => b.Bookings)
                    .GroupBy(b => new { b.OwnerId, b.Owner.FullName })
                    .Select(g => new TopOwner
                    {
                        OwnerId = g.Key.OwnerId,
                        OwnerName = g.Key.FullName,
                        BikeCount = g.Count(),
                        BookingCount = g.SelectMany(b => b.Bookings).Count(),
                        TotalEarnings = g.SelectMany(b => b.Bookings)
                            .Where(booking => booking.BookingStatus == "Completed")
                            .Sum(booking => booking.TotalAmount * 0.9m)
                    })
                    .OrderByDescending(o => o.TotalEarnings)
                    .Take(10)
                    .ToListAsync();

                TopOwners = topOwnersData;

                Log.Information("Admin analytics loaded successfully for {Days} days", Days);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading admin analytics");
            }

            return Page();
        }
    }
}

