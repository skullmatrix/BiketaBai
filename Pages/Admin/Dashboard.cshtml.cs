using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Admin;

public class AdminDashboardModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public AdminDashboardModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public int TotalUsers { get; set; }
    public int RentersCount { get; set; }
    public int OwnersCount { get; set; }
    public int TotalBikes { get; set; }
    public int AvailableBikes { get; set; }
    public int TotalBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int TodayBookings { get; set; }
    public int WeekBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalWalletBalance { get; set; }
    public decimal TotalCO2Saved { get; set; }

    public List<User> RecentUsers { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
    public List<Bike> TopBikes { get; set; } = new();
    public Dictionary<int, decimal> BikeEarnings { get; set; } = new();
    public List<BikeTypeStat> BikeTypeStats { get; set; } = new();

    public class BikeTypeStat
    {
        public string TypeName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        // User statistics
        TotalUsers = await _context.Users.CountAsync();
        RentersCount = await _context.Users.Where(u => u.IsRenter).CountAsync();
        OwnersCount = await _context.Users.Where(u => u.IsOwner).CountAsync();

        // Bike statistics
        TotalBikes = await _context.Bikes.CountAsync();
        AvailableBikes = await _context.Bikes.Where(b => b.AvailabilityStatusId == 1).CountAsync();

        // Booking statistics
        TotalBookings = await _context.Bookings.CountAsync();
        ActiveBookings = await _context.Bookings.Where(b => b.BookingStatusId == 2).CountAsync();
        CompletedBookings = await _context.Bookings.Where(b => b.BookingStatusId == 3).CountAsync();

        var today = DateTime.UtcNow.Date;
        TodayBookings = await _context.Bookings
            .Where(b => b.CreatedAt.Date == today)
            .CountAsync();

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        WeekBookings = await _context.Bookings
            .Where(b => b.CreatedAt >= weekAgo)
            .CountAsync();

        // Revenue (10% service fee from all completed bookings)
        TotalRevenue = await _context.Bookings
            .Where(b => b.BookingStatusId == 3)
            .SumAsync(b => b.ServiceFee);

        // Wallet statistics
        TotalWalletBalance = await _context.Wallets.SumAsync(w => w.Balance);

        // CO2 saved
        var totalKmSaved = await _context.Bookings
            .Where(b => b.DistanceSavedKm.HasValue)
            .SumAsync(b => b.DistanceSavedKm ?? 0);
        TotalCO2Saved = totalKmSaved * 0.2m;

        // Recent users
        RecentUsers = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Recent bookings
        RecentBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Top bikes by earnings
        var bookingsWithBikes = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Where(b => b.BookingStatusId == 3) // Completed
            .ToListAsync();

        var bikeEarningsDict = bookingsWithBikes
            .GroupBy(b => b.BikeId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.TotalAmount * 0.9m)); // 90% after service fee

        BikeEarnings = bikeEarningsDict;

        TopBikes = await _context.Bikes
            .Include(b => b.Owner)
            .Where(b => bikeEarningsDict.Keys.Contains(b.BikeId))
            .ToListAsync();

        TopBikes = TopBikes
            .OrderByDescending(b => BikeEarnings.ContainsKey(b.BikeId) ? BikeEarnings[b.BikeId] : 0)
            .ToList();

        // Bike type distribution
        BikeTypeStats = await _context.Bikes
            .Include(b => b.BikeType)
            .GroupBy(b => b.BikeType.TypeName)
            .Select(g => new BikeTypeStat
            {
                TypeName = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(s => s.Count)
            .ToListAsync();

        return Page();
    }
}

