using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Services;
using BiketaBai.Helpers;
using BiketaBai.Data;
using Serilog;

namespace BiketaBai.Pages.Api;

[IgnoreAntiforgeryToken]
public class LocationTrackingModel : PageModel
{
    private readonly GeofencingService _geofencingService;
    private readonly BiketaBaiDbContext _context;

    public LocationTrackingModel(
        GeofencingService geofencingService,
        BiketaBaiDbContext context)
    {
        _geofencingService = geofencingService;
        _context = context;
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

            // Track location
            var (isWithin, distanceKm, warningMessage) = await _geofencingService.TrackLocationAsync(
                request.BookingId,
                request.Latitude,
                request.Longitude);

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

