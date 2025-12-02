using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class LostBikeDetailsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public LostBikeDetailsModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public Booking? Booking { get; set; }
    public LocationTracking? LastLocation { get; set; }
    public double? StoreLatitude { get; set; }
    public double? StoreLongitude { get; set; }
    public decimal? GeofenceRadiusKm { get; set; }

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Get booking with all related data
        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value && b.IsReportedLost);

        if (Booking == null)
            return NotFound();

        // Get owner's store location
        var owner = await _context.Users.FindAsync(userId.Value);
        if (owner != null)
        {
            StoreLatitude = owner.StoreLatitude;
            StoreLongitude = owner.StoreLongitude;
            GeofenceRadiusKm = owner.GeofenceRadiusKm;
        }

        // Get last known location
        LastLocation = await _context.LocationTracking
            .Where(lt => lt.BookingId == bookingId)
            .OrderByDescending(lt => lt.TrackedAt)
            .FirstOrDefaultAsync();

        return Page();
    }
}

