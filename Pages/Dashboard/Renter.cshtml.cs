using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Dashboard;

public class RenterDashboardModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BookingService _bookingService;

    public RenterDashboardModel(BiketaBaiDbContext context, BookingService bookingService)
    {
        _context = context;
        _bookingService = bookingService;
    }

    public List<Booking> ActiveBookings { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
    public int ActiveRentalsCount { get; set; }
    public int TotalRentalsCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal CO2Saved { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Get active bookings
        ActiveBookings = await _bookingService.GetUserBookingsAsync(userId.Value, asRenter: true, statusId: 2); // Active

        // Get recent bookings
        RecentBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Where(b => b.RenterId == userId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Calculate stats
        ActiveRentalsCount = ActiveBookings.Count;
        TotalRentalsCount = await _context.Bookings
            .Where(b => b.RenterId == userId.Value && b.BookingStatus == "Completed")
            .CountAsync();
        
        TotalSpent = await _context.Bookings
            .Where(b => b.RenterId == userId.Value && b.BookingStatus == "Completed")
            .SumAsync(b => b.TotalAmount);

        var totalKmSaved = await _context.Bookings
            .Where(b => b.RenterId == userId.Value && b.DistanceSavedKm.HasValue)
            .SumAsync(b => b.DistanceSavedKm ?? 0);
        CO2Saved = totalKmSaved * 0.2m;

        return Page();
    }

    // Removed OnPostCompleteAsync - renters can no longer mark bikes as returned
    // Only owners can confirm bike returns
}

