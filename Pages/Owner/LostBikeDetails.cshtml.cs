using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

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

    public async Task<IActionResult> OnPostMarkAsFoundAsync(int bookingId)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "You must be logged in";
                return RedirectToPage("/Account/Login");
            }

            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value && b.IsReportedLost);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found or you don't have permission";
                return RedirectToPage("/Owner/MyBikes");
            }

            // Mark as found: Remove lost status and mark as completed
            booking.IsReportedLost = false;
            booking.BookingStatusId = 3; // Completed
            booking.ActualReturnDate = DateTime.UtcNow;
            booking.OwnerConfirmedAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send notification to renter
            var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
            await notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Bike Found - Return Confirmed",
                $"The bike from booking #{bookingId.ToString("D6")} has been found and marked as returned. Thank you for your cooperation.",
                "Booking",
                $"/Bookings/Details/{bookingId}"
            );

            TempData["SuccessMessage"] = $"Bike marked as found and return confirmed for booking #{bookingId.ToString("D6")}";
            return RedirectToPage("/Owner/MyBikes");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error marking bike as found: {ex.Message}";
            return RedirectToPage();
        }
    }
}
