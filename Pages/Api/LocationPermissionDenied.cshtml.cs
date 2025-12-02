using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Services;
using Serilog;

namespace BiketaBai.Pages.Api;

[IgnoreAntiforgeryToken]
public class LocationPermissionDeniedModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public LocationPermissionDeniedModel(
        BiketaBaiDbContext context,
        NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
            {
                Response.StatusCode = 401;
                Response.ContentType = "application/json";
                await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized" }));
                return new EmptyResult();
            }

            // Read request body
            using var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var request = System.Text.Json.JsonSerializer.Deserialize<LocationPermissionDeniedRequest>(
                body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || request.BookingId <= 0)
            {
                Response.StatusCode = 400;
                Response.ContentType = "application/json";
                await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Invalid request" }));
                return new EmptyResult();
            }

            // Validate booking belongs to user
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.RenterId == userId.Value);

            if (booking == null)
            {
                Response.StatusCode = 404;
                Response.ContentType = "application/json";
                await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Booking not found" }));
                return new EmptyResult();
            }

            // Check if already denied (avoid duplicate notifications)
            if (!booking.LocationPermissionDeniedAt.HasValue)
            {
                // Mark location permission as denied
                booking.LocationPermissionGranted = false;
                booking.LocationPermissionDeniedAt = DateTime.UtcNow;
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Notify owner about location permission denial
                await _notificationService.CreateNotificationAsync(
                    booking.Bike.OwnerId,
                    "Location Permission Denied",
                    $"Renter {booking.Renter.FullName} denied location access for booking #{booking.BookingId} ({booking.Bike.Brand} {booking.Bike.Model}). The rental cannot be fully validated without location tracking.",
                    "Booking",
                    $"/Owner/RentalRequests"
                );

                Log.Warning("Location permission denied for booking {BookingId} by renter {RenterId}. Owner {OwnerId} notified.",
                    booking.BookingId, booking.RenterId, booking.Bike.OwnerId);
            }

            Response.ContentType = "application/json";
            var response = new
            {
                success = true,
                message = "Owner notified about location permission denial"
            };

            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling location permission denial");
            Response.StatusCode = 500;
            Response.ContentType = "application/json";
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Internal server error" }));
            return new EmptyResult();
        }
    }

    private class LocationPermissionDeniedRequest
    {
        public int BookingId { get; set; }
    }
}

