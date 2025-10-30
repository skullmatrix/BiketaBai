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

    // User Statistics
    public int TotalUsers { get; set; }
    public int RentersCount { get; set; }
    public int OwnersCount { get; set; }
    public int SuspendedUsers { get; set; }
    
    // Bike Statistics
    public int TotalBikes { get; set; }
    public int AvailableBikes { get; set; }
    
    // Booking Statistics
    public int TotalBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int TodayBookings { get; set; }
    public int WeekBookings { get; set; }
    
    // Financial Statistics
    public decimal TotalRevenue { get; set; }
    public decimal TotalWalletBalance { get; set; }
    
    // Environmental Impact
    public decimal TotalCO2Saved { get; set; }
    
    // Verification & Admin Actions
    public int PendingOwnerVerifications { get; set; }
    public List<User> PendingVerifications { get; set; } = new();
    
    // Recent Activity
    public List<User> RecentUsers { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
    public List<Bike> RecentBikes { get; set; } = new();
    
    // Top Performers
    public List<Bike> TopBikes { get; set; } = new();
    public Dictionary<int, decimal> BikeEarnings { get; set; } = new();
    public List<BikeTypeStat> BikeTypeStats { get; set; } = new();

    public class BikeTypeStat
    {
        public string TypeName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public string GetRoleBadge(User user)
    {
        var badges = new List<string>();
        
        if (user.IsAdmin) badges.Add("<span class='admin-badge red'>Admin</span>");
        if (user.IsOwner) badges.Add("<span class='admin-badge amber'>Owner</span>");
        if (user.IsRenter) badges.Add("<span class='admin-badge blue'>Renter</span>");
        
        return string.Join(" ", badges);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        // User statistics
        var allUsers = await _context.Users.ToListAsync();
        TotalUsers = allUsers.Count;
        RentersCount = allUsers.Count(u => u.IsRenter);
        OwnersCount = allUsers.Count(u => u.IsOwner);
        SuspendedUsers = allUsers.Count(u => u.IsSuspended);

        // Pending owner verifications
        PendingOwnerVerifications = allUsers.Count(u => u.IsOwner && u.VerificationStatus == "Pending");
        PendingVerifications = allUsers
            .Where(u => u.IsOwner && u.VerificationStatus == "Pending")
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .ToList();

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
        RecentUsers = allUsers
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToList();

        // Recent bookings
        RecentBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.BookingStatus)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Recent bikes
        RecentBikes = await _context.Bikes
            .Include(b => b.Owner)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
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

