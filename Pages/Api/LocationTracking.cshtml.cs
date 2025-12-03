using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using BiketaBai.Services;
using BiketaBai.Helpers;
using BiketaBai.Data;
using BiketaBai.Hubs;
using Serilog;

namespace BiketaBai.Pages.Api;

[IgnoreAntiforgeryToken]
public class LocationTrackingModel : PageModel
{
    private readonly GeofencingService _geofencingService;
    private readonly BiketaBaiDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public LocationTrackingModel(
        GeofencingService geofencingService,
        BiketaBaiDbContext context,
        IHubContext<NotificationHub> hubContext)
    {
        _geofencingService = geofencingService;
        _context = context;
        _hubContext = hubContext;
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

            var request = System.Text.Json.JsonSerializer.Deserialize<LocationTrackingRequest>(
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
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.RenterId == userId.Value);

            if (booking == null)
            {
                Response.StatusCode = 404;
                Response.ContentType = "application/json";
                await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Booking not found" }));
                return new EmptyResult();
            }

            // Mark location permission as granted (first time location is sent)
            if (!booking.LocationPermissionGranted)
            {
                booking.LocationPermissionGranted = true;
                booking.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Track location
            var (isWithin, distanceKm, warningMessage) = await _geofencingService.TrackLocationAsync(
                request.BookingId,
                request.Latitude,
                request.Longitude);

            // Get booking with owner info for SignalR broadcast
            var bookingWithOwner = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId);

            // Broadcast location update to owner via SignalR
            if (bookingWithOwner != null)
            {
                var ownerId = bookingWithOwner.Bike.OwnerId;
                try
                {
                    await _hubContext.Clients.Group($"user_{ownerId}").SendAsync("ReceiveLocationUpdate", new
                    {
                        bookingId = request.BookingId,
                        latitude = request.Latitude,
                        longitude = request.Longitude,
                        distanceKm = Math.Round(distanceKm, 2),
                        isWithinGeofence = isWithin,
                        renterName = bookingWithOwner.Renter.FullName,
                        bikeName = $"{bookingWithOwner.Bike.Brand} {bookingWithOwner.Bike.Model}",
                        trackedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });

                    Log.Information("Location update broadcasted to owner {OwnerId} for booking {BookingId}. Distance: {Distance}km, Within: {Within}", 
                        ownerId, request.BookingId, Math.Round(distanceKm, 2), isWithin);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error broadcasting location update to owner {OwnerId} for booking {BookingId}", 
                        ownerId, request.BookingId);
                }
            }

            Response.ContentType = "application/json";
            var response = new
            {
                success = true,
                isWithinGeofence = isWithin,
                distanceKm = Math.Round(distanceKm, 2),
                warningMessage = warningMessage
            };

            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error tracking location");
            Response.StatusCode = 500;
            Response.ContentType = "application/json";
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Internal server error" }));
            return new EmptyResult();
        }
    }

    private class LocationTrackingRequest
    {
        public int BookingId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}

