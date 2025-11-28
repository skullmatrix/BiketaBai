using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Services;
using BiketaBai.Models;

namespace BiketaBai.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TestGeofencingModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly GeofencingService _geofencingService;

    public TestGeofencingModel(BiketaBaiDbContext context, GeofencingService geofencingService)
    {
        _context = context;
        _geofencingService = geofencingService;
    }

    [BindProperty]
    public int BookingId { get; set; }

    [BindProperty]
    public double TestLatitude { get; set; }

    [BindProperty]
    public double TestLongitude { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TestResult { get; set; }

    public List<Booking> ActiveBookings { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadActiveBookingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTestLocationAsync()
    {
        if (BookingId <= 0)
        {
            ErrorMessage = "Please select a booking";
            await LoadActiveBookingsAsync();
            return Page();
        }

        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                ErrorMessage = "Booking not found";
                await LoadActiveBookingsAsync();
                return Page();
            }

            // Test geofence check
            var (isWithin, distanceKm, warningMessage) = await _geofencingService.TrackLocationAsync(
                BookingId,
                TestLatitude,
                TestLongitude);

            var owner = booking.Bike.Owner;
            var radius = owner.GeofenceRadiusKm ?? _geofencingService.GetDefaultGeofenceRadius();

            TestResult = $"✅ Test Completed!\n\n" +
                        $"Booking: #{BookingId} - {booking.Bike.Brand} {booking.Bike.Model}\n" +
                        $"Renter: {booking.Renter.FullName}\n" +
                        $"Store: {owner.StoreName ?? "N/A"}\n" +
                        $"Store Location: {owner.StoreLatitude?.ToString("F6") ?? "N/A"}, {owner.StoreLongitude?.ToString("F6") ?? "N/A"}\n" +
                        $"Test Location: {TestLatitude:F6}, {TestLongitude:F6}\n" +
                        $"Geofence Radius: {radius} km\n" +
                        $"Distance from Store: {distanceKm:F2} km\n" +
                        $"Status: {(isWithin ? "✅ WITHIN geofence" : "⚠️ OUTSIDE geofence")}\n" +
                        $"{(warningMessage != null ? $"SMS Status: {warningMessage}" : "")}";

            if (isWithin)
            {
                SuccessMessage = "Location is within geofence. No SMS warning sent.";
            }
            else
            {
                if (!string.IsNullOrEmpty(warningMessage))
                {
                    SuccessMessage = $"Location is outside geofence. SMS warning sent to {booking.Renter.Phone}";
                }
                else
                {
                    SuccessMessage = "Location is outside geofence. SMS may have been sent recently (15-minute cooldown).";
                }
            }

            await LoadActiveBookingsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            await LoadActiveBookingsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostTestDistanceAsync()
    {
        if (BookingId <= 0)
        {
            ErrorMessage = "Please select a booking";
            await LoadActiveBookingsAsync();
            return Page();
        }

        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                ErrorMessage = "Booking not found";
                await LoadActiveBookingsAsync();
                return Page();
            }

            var owner = booking.Bike.Owner;
            var (storeLat, storeLon) = await _geofencingService.GetStoreLocationAsync(owner.UserId);

            if (!storeLat.HasValue || !storeLon.HasValue)
            {
                ErrorMessage = "Store location not available. Please set store address first.";
                await LoadActiveBookingsAsync();
                return Page();
            }

            var distance = _geofencingService.CalculateDistance(
                storeLat.Value, 
                storeLon.Value, 
                TestLatitude, 
                TestLongitude);

            var radius = owner.GeofenceRadiusKm ?? _geofencingService.GetDefaultGeofenceRadius();
            var isWithin = distance <= (double)radius;

            TestResult = $"📏 Distance Calculation Test\n\n" +
                        $"Store Location: {storeLat.Value:F6}, {storeLon.Value:F6}\n" +
                        $"Test Location: {TestLatitude:F6}, {TestLongitude:F6}\n" +
                        $"Calculated Distance: {distance:F2} km\n" +
                        $"Geofence Radius: {radius} km\n" +
                        $"Status: {(isWithin ? "✅ WITHIN" : "⚠️ OUTSIDE")} geofence";

            SuccessMessage = $"Distance calculated: {distance:F2} km";
            await LoadActiveBookingsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            await LoadActiveBookingsAsync();
            return Page();
        }
    }

    private async Task LoadActiveBookingsAsync()
    {
        ActiveBookings = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .Where(b => b.BookingStatusId == 2) // Active
            .OrderByDescending(b => b.CreatedAt)
            .Take(20)
            .ToListAsync();
    }
}

