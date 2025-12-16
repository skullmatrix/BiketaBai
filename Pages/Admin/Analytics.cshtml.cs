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
                var startDate = DateTime.UtcNow.AddDays(-Days).Date;
                var previousPeriodStart = DateTime.UtcNow.AddDays(-Days * 2).Date;
                var previousPeriodEnd = DateTime.UtcNow.AddDays(-Days).Date;

                // Overall Statistics
                TotalUsers = await _context.Users.CountAsync();
                TotalBikes = await _context.Bikes.CountAsync();
                TotalBookings = await _context.Bookings.CountAsync();
                
                var revenueResult = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed")
                    .Select(b => b.ServiceFee)
                    .ToListAsync();
                TotalRevenue = revenueResult.Sum();

                var totalKmSaved = await _context.Bookings
                    .Where(b => b.DistanceSavedKm.HasValue)
                    .Select(b => b.DistanceSavedKm ?? 0)
                    .ToListAsync();
                TotalCO2Saved = totalKmSaved.Sum() * 0.2m;

                // Daily Bookings (last N days)
                var dailyBookingsData = await _context.Bookings
                    .Where(b => b.CreatedAt >= startDate)
                    .ToListAsync();

                DailyBookings = dailyBookingsData
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new DailyStat
                    {
                        Date = g.Key.ToString("MMM dd"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.TotalAmount)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                // Daily Revenue (from completed bookings)
                var dailyRevenueData = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= startDate)
                    .ToListAsync();

                DailyRevenue = dailyRevenueData
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new DailyStat
                    {
                        Date = g.Key.ToString("MMM dd"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.ServiceFee)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                // Monthly Bookings (last 12 months)
                var monthlyBookingsRaw = await _context.Bookings
                    .Where(b => b.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
                    .ToListAsync();

                MonthlyBookings = monthlyBookingsRaw
                    .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                    .Select(g => new MonthlyStat
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.TotalAmount)
                    })
                    .OrderBy(m => m.Month)
                    .ToList();

                // Monthly Revenue
                var monthlyRevenueRaw = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= DateTime.UtcNow.AddMonths(-12))
                    .ToListAsync();

                MonthlyRevenue = monthlyRevenueRaw
                    .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                    .Select(g => new MonthlyStat
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Count = g.Count(),
                        Amount = g.Sum(b => b.ServiceFee)
                    })
                    .OrderBy(m => m.Month)
                    .ToList();

                // Payment Method Statistics
                var paymentStatsRaw = await _context.Payments
                    .Where(p => p.PaymentStatus == "Completed")
                    .ToListAsync();

                var paymentStats = paymentStatsRaw
                    .GroupBy(p => p.PaymentMethod ?? "Unknown")
                    .Select(g => new PaymentMethodStat
                    {
                        PaymentMethod = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(p => p.Amount)
                    })
                    .ToList();

                var totalPayments = paymentStats.Sum(p => p.Count);
                foreach (var stat in paymentStats)
                {
                    stat.Percentage = totalPayments > 0 ? (decimal)stat.Count / totalPayments * 100 : 0;
                }
                PaymentMethodStats = paymentStats.OrderByDescending(p => p.Count).ToList();

                // Bike Type Statistics
                var bikeTypeStatsRaw = await _context.Bikes
                    .Include(b => b.BikeType)
                    .Where(b => b.BikeType != null)
                    .ToListAsync();

                var bikeTypeStats = bikeTypeStatsRaw
                    .GroupBy(b => b.BikeType!.TypeName)
                    .Select(g => new BikeTypeStat
                    {
                        TypeName = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                var totalBikesForType = bikeTypeStats.Sum(b => b.Count);
                foreach (var stat in bikeTypeStats)
                {
                    stat.Percentage = totalBikesForType > 0 ? (decimal)stat.Count / totalBikesForType * 100 : 0;
                }
                BikeTypeStats = bikeTypeStats.OrderByDescending(b => b.Count).ToList();

                // Booking Status Statistics
                var statusStatsRaw = await _context.Bookings
                    .ToListAsync();

                var statusStats = statusStatsRaw
                    .GroupBy(b => b.BookingStatus ?? "Unknown")
                    .Select(g => new StatusStat
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

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

                var currentPeriodRevenueList = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= startDate)
                    .Select(b => b.ServiceFee)
                    .ToListAsync();
                var currentPeriodRevenue = currentPeriodRevenueList.Sum();
                
                var previousPeriodRevenueList = await _context.Bookings
                    .Where(b => b.BookingStatus == "Completed" && b.CreatedAt >= previousPeriodStart && b.CreatedAt < previousPeriodEnd)
                    .Select(b => b.ServiceFee)
                    .ToListAsync();
                var previousPeriodRevenue = previousPeriodRevenueList.Sum();
                
                RevenueGrowthRate = previousPeriodRevenue > 0 
                    ? ((currentPeriodRevenue - previousPeriodRevenue) / previousPeriodRevenue) * 100 
                    : (currentPeriodRevenue > 0 ? 100 : 0);

                // Top Bikes
                var topBikesData = await _context.Bookings
                    .Include(b => b.Bike)
                        .ThenInclude(bike => bike.Owner)
                    .Include(b => b.Ratings)
                    .Where(b => b.BookingStatus == "Completed" && b.Bike != null && b.Bike.Owner != null)
                    .ToListAsync();

                var topBikesGrouped = topBikesData
                    .GroupBy(b => new { 
                        b.BikeId, 
                        Brand = b.Bike!.Brand ?? "Unknown", 
                        Model = b.Bike.Model ?? "Unknown", 
                        OwnerName = b.Bike.Owner!.FullName ?? "Unknown" 
                    })
                    .Select(g => new TopBike
                    {
                        BikeId = g.Key.BikeId,
                        BikeName = $"{g.Key.Brand} {g.Key.Model}",
                        OwnerName = g.Key.OwnerName,
                        BookingCount = g.Count(),
                        TotalEarnings = g.Sum(b => b.TotalAmount * 0.9m), // 90% after service fee
                        AvgRating = g.SelectMany(b => b.Ratings)
                            .Where(r => r.RatingValue > 0)
                            .Select(r => (decimal)r.RatingValue)
                            .DefaultIfEmpty(0)
                            .Average()
                    })
                    .OrderByDescending(b => b.TotalEarnings)
                    .Take(10)
                    .ToList();

                TopBikes = topBikesGrouped;

                // Top Owners
                var topOwnersRaw = await _context.Bikes
                    .Include(b => b.Owner)
                    .Include(b => b.Bookings)
                    .Where(b => b.Owner != null)
                    .ToListAsync();

                var topOwnersData = topOwnersRaw
                    .GroupBy(b => new { b.OwnerId, OwnerName = b.Owner!.FullName })
                    .Select(g => new TopOwner
                    {
                        OwnerId = g.Key.OwnerId,
                        OwnerName = g.Key.OwnerName,
                        BikeCount = g.Count(),
                        BookingCount = g.SelectMany(b => b.Bookings).Count(),
                        TotalEarnings = g.SelectMany(b => b.Bookings)
                            .Where(booking => booking.BookingStatus == "Completed")
                            .Sum(booking => booking.TotalAmount * 0.9m)
                    })
                    .OrderByDescending(o => o.TotalEarnings)
                    .Take(10)
                    .ToList();

                TopOwners = topOwnersData;

                Log.Information("Admin analytics loaded successfully for {Days} days. Users: {Users}, Bikes: {Bikes}, Bookings: {Bookings}, Revenue: {Revenue}", 
                    Days, TotalUsers, TotalBikes, TotalBookings, TotalRevenue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading admin analytics: {Message}", ex.Message);
                // Set default values to prevent null reference errors
                TotalUsers = 0;
                TotalBikes = 0;
                TotalBookings = 0;
                TotalRevenue = 0;
                TotalCO2Saved = 0;
                DailyBookings = new List<DailyStat>();
                DailyRevenue = new List<DailyStat>();
                MonthlyBookings = new List<MonthlyStat>();
                MonthlyRevenue = new List<MonthlyStat>();
                PaymentMethodStats = new List<PaymentMethodStat>();
                BikeTypeStats = new List<BikeTypeStat>();
                BookingStatusStats = new List<StatusStat>();
                TopBikes = new List<TopBike>();
                TopOwners = new List<TopOwner>();
            }

            return Page();
        }
    }
}

