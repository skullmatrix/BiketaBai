using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class TrackRentersModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public TrackRentersModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public List<Booking> ActiveBookings { get; set; } = new();
    public Dictionary<int, LocationTracking?> LatestLocations { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Get all active bookings for this owner's bikes
        ActiveBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.LocationTracking.OrderByDescending(lt => lt.TrackedAt).Take(1))
            .Where(b => b.Bike.OwnerId == userId.Value && b.BookingStatusId == 2) // Active
            .OrderByDescending(b => b.StartDate)
            .ToListAsync();

        // Get latest location for each booking
        foreach (var booking in ActiveBookings)
        {
            var latestLocation = await _context.LocationTracking
                .Where(lt => lt.BookingId == booking.BookingId)
                .OrderByDescending(lt => lt.TrackedAt)
                .FirstOrDefaultAsync();
            
            LatestLocations[booking.BookingId] = latestLocation;
        }

        return Page();
    }
}

