using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Hubs;
using BiketaBai.Models;

namespace BiketaBai.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TestLocationTrackingModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public TestLocationTrackingModel(BiketaBaiDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public List<Booking> ActiveBookings { get; set; } = new();
    public int? SelectedBookingId { get; set; }
    public string? TestMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Get active bookings for testing
        ActiveBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Where(b => b.BookingStatus == "Active")
            .OrderByDescending(b => b.CreatedAt)
            .Take(20)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostSendTestLocationAsync(int bookingId, double latitude, double longitude)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found";
                return RedirectToPage();
            }

            var ownerId = booking.Bike.OwnerId;

            // Send test location update via SignalR
            await _hubContext.Clients.Group($"user_{ownerId}").SendAsync("ReceiveLocationUpdate", new
            {
                bookingId = bookingId,
                latitude = latitude,
                longitude = longitude,
                distanceKm = 0.5, // Test distance
                isWithinGeofence = true,
                renterName = booking.Renter.FullName,
                bikeName = $"{booking.Bike.Brand} {booking.Bike.Model}",
                trackedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });

            TempData["SuccessMessage"] = $"Test location sent to owner (User ID: {ownerId}) for booking #{bookingId}";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }
}

