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
    public double? StoreLatitude { get; set; }
    public double? StoreLongitude { get; set; }
    public decimal? GeofenceRadiusKm { get; set; }
    public string? StoreName { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Get primary store for owner
        var primaryStore = await _context.Stores
            .FirstOrDefaultAsync(s => s.OwnerId == userId.Value && s.IsPrimary && !s.IsDeleted);
        
        if (primaryStore != null)
        {
            StoreLatitude = primaryStore.StoreLatitude;
            StoreLongitude = primaryStore.StoreLongitude;
            GeofenceRadiusKm = primaryStore.GeofenceRadiusKm;
            StoreName = primaryStore.StoreName;
        }

        // Get all active bookings for this owner's bikes
        ActiveBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.LocationTracking.OrderByDescending(lt => lt.TrackedAt).Take(1))
            .Where(b => b.Bike.OwnerId == userId.Value && b.BookingStatus == "Active")
            .OrderByDescending(b => b.StartDate)
            .ToListAsync();

        // Get latest location for each booking (optimized query)
        var bookingIds = ActiveBookings.Select(b => b.BookingId).ToList();
        if (bookingIds.Any())
        {
            var allLatestLocations = await _context.LocationTracking
                .Where(lt => bookingIds.Contains(lt.BookingId))
                .GroupBy(lt => lt.BookingId)
                .Select(g => g.OrderByDescending(lt => lt.TrackedAt).First())
                .ToListAsync();
            
            foreach (var location in allLatestLocations)
            {
                LatestLocations[location.BookingId] = location;
            }
        }

        return Page();
    }
}

