using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Api;

[IgnoreAntiforgeryToken]
public class LocationPermissionGrantedModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public LocationPermissionGrantedModel(BiketaBaiDbContext context)
    {
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

            var request = System.Text.Json.JsonSerializer.Deserialize<LocationPermissionGrantedRequest>(
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

            // Mark location permission as granted
            if (!booking.LocationPermissionGranted)
            {
                booking.LocationPermissionGranted = true;
                booking.LocationPermissionDeniedAt = null; // Clear denial if previously denied
                booking.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Log.Information("Location permission granted for booking {BookingId} by renter {RenterId}.",
                    booking.BookingId, booking.RenterId);
            }

            Response.ContentType = "application/json";
            var response = new
            {
                success = true,
                message = "Location permission granted"
            };

            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error recording location permission grant");
            Response.StatusCode = 500;
            Response.ContentType = "application/json";
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "Internal server error" }));
            return new EmptyResult();
        }
    }

    private class LocationPermissionGrantedRequest
    {
        public int BookingId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}

