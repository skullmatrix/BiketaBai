using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Bookings;

[Authorize]
public class TrackLocationModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly GeofencingService _geofencingService;

    public TrackLocationModel(BiketaBaiDbContext context, GeofencingService geofencingService)
    {
        _context = context;
        _geofencingService = geofencingService;
    }

    public Booking? Booking { get; set; }
    public User? Owner { get; set; }
    public Store? PrimaryStore { get; set; }
    public double? StoreLatitude { get; set; }
    public double? StoreLongitude { get; set; }
    public decimal GeofenceRadiusKm { get; set; }
    public LocationTracking? LatestLocation { get; set; }
    public List<LocationTracking> LocationHistory { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Only allow tracking for active bookings
        if (Booking.BookingStatus != "Active")
        {
            TempData["ErrorMessage"] = "Location tracking is only available for active bookings.";
            return RedirectToPage("/Dashboard/Renter");
        }

        Owner = Booking.Bike.Owner;
        
        // Get primary store
        PrimaryStore = await _context.Stores
            .FirstOrDefaultAsync(s => s.OwnerId == Owner.UserId && s.IsPrimary && !s.IsDeleted);
        
        // Get store location from Store model or geofencing service
        if (PrimaryStore != null)
        {
            StoreLatitude = PrimaryStore.StoreLatitude;
            StoreLongitude = PrimaryStore.StoreLongitude;
            GeofenceRadiusKm = PrimaryStore.GeofenceRadiusKm ?? _geofencingService.GetDefaultGeofenceRadius();
        }
        else
        {
            // Fallback to geofencing service if no store found
            var (lat, lon) = await _geofencingService.GetStoreLocationAsync(Owner.UserId);
            StoreLatitude = lat;
            StoreLongitude = lon;
            GeofenceRadiusKm = _geofencingService.GetDefaultGeofenceRadius();
        }

        // Get latest location tracking
        LatestLocation = await _context.LocationTracking
            .Where(lt => lt.BookingId == bookingId)
            .OrderByDescending(lt => lt.TrackedAt)
            .FirstOrDefaultAsync();

        // Get location history (last 100 points for trail)
        LocationHistory = await _context.LocationTracking
            .Where(lt => lt.BookingId == bookingId)
            .OrderByDescending(lt => lt.TrackedAt)
            .Take(100)
            .OrderBy(lt => lt.TrackedAt)
            .ToListAsync();

        return Page();
    }
}

