using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BiketaBai.Services;

public class GeofencingService
{
    private readonly BiketaBaiDbContext _context;
    private readonly SmsService _smsService;
    private readonly AddressValidationService _addressValidationService;
    private readonly IConfiguration _configuration;

    public GeofencingService(
        BiketaBaiDbContext context,
        SmsService smsService,
        AddressValidationService addressValidationService,
        IConfiguration configuration)
    {
        _context = context;
        _smsService = smsService;
        _addressValidationService = addressValidationService;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the default geofence radius in kilometers
    /// </summary>
    public decimal GetDefaultGeofenceRadius()
    {
        return _configuration.GetValue<decimal>("AppSettings:GeofenceRadiusKm", 10.0m); // Default 10km
    }

    /// <summary>
    /// Gets the owner's store location coordinates
    /// </summary>
    public async Task<(double? latitude, double? longitude)> GetStoreLocationAsync(int ownerId)
    {
        var owner = await _context.Users.FindAsync(ownerId);
        if (owner == null || string.IsNullOrWhiteSpace(owner.StoreAddress))
            return (null, null);

        // Check if coordinates are already stored
        if (owner.StoreLatitude.HasValue && owner.StoreLongitude.HasValue)
        {
            return (owner.StoreLatitude.Value, owner.StoreLongitude.Value);
        }

        // Geocode the address if coordinates not available
        var geocodeResult = await _addressValidationService.ValidateAddressAsync(owner.StoreAddress);
        if (geocodeResult.IsValid && geocodeResult.Latitude.HasValue && geocodeResult.Longitude.HasValue)
        {
            // Store coordinates for future use
            owner.StoreLatitude = geocodeResult.Latitude.Value;
            owner.StoreLongitude = geocodeResult.Longitude.Value;
            await _context.SaveChangesAsync();

            return (geocodeResult.Latitude.Value, geocodeResult.Longitude.Value);
        }

        return (null, null);
    }

    /// <summary>
    /// Calculates distance between two coordinates using Haversine formula
    /// Returns distance in kilometers
    /// </summary>
    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = R * c;

        return distance;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Checks if a location is within the geofence radius
    /// </summary>
    public async Task<(bool isWithin, double distanceKm)> CheckGeofenceAsync(
        int bookingId, 
        double renterLatitude, 
        double renterLongitude)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return (false, 0);

        var owner = booking.Bike.Owner;
        var (storeLat, storeLon) = await GetStoreLocationAsync(owner.UserId);

        if (!storeLat.HasValue || !storeLon.HasValue)
        {
            Log.Warning("Store location not available for owner {OwnerId}. Cannot check geofence.", owner.UserId);
            return (true, 0); // Allow if store location not available
        }

        var distance = CalculateDistance(storeLat.Value, storeLon.Value, renterLatitude, renterLongitude);
        var radius = owner.GeofenceRadiusKm ?? GetDefaultGeofenceRadius();

        var isWithin = (decimal)distance <= radius;

        return (isWithin, distance);
    }

    /// <summary>
    /// Records renter's location and checks geofence
    /// </summary>
    public async Task<(bool isWithin, double distanceKm, string? warningMessage)> TrackLocationAsync(
        int bookingId,
        double latitude,
        double longitude)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return (false, 0, "Booking not found");

        // Only track active bookings
        if (booking.BookingStatusId != 2) // 2 = Active
        {
            return (true, 0, null);
        }

        var (isWithin, distanceKm) = await CheckGeofenceAsync(bookingId, latitude, longitude);

        // Record location tracking
        var tracking = new LocationTracking
        {
            BookingId = bookingId,
            Latitude = latitude,
            Longitude = longitude,
            DistanceFromStoreKm = (decimal)distanceKm,
            IsWithinGeofence = isWithin,
            TrackedAt = DateTime.UtcNow
        };

        _context.LocationTracking.Add(tracking);

        // Check if we need to send warning
        string? warningMessage = null;
        if (!isWithin)
        {
            // Check if we've already sent a warning recently (within last 15 minutes)
            var lastWarning = await _context.LocationTracking
                .Where(t => t.BookingId == bookingId && !t.IsWithinGeofence)
                .OrderByDescending(t => t.TrackedAt)
                .FirstOrDefaultAsync();

            var shouldSendWarning = lastWarning == null || 
                (DateTime.UtcNow - lastWarning.TrackedAt).TotalMinutes >= 15;

            if (shouldSendWarning && !string.IsNullOrWhiteSpace(booking.Renter.Phone))
            {
                var owner = booking.Bike.Owner;
                var radius = owner.GeofenceRadiusKm ?? GetDefaultGeofenceRadius();
                var message = $"⚠️ Geofence Alert: You are {distanceKm:F1}km away from {owner.StoreName ?? "the store location"}. " +
                             $"Please return within {radius}km radius. Current distance: {distanceKm:F1}km.";

                var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
                if (smsSent)
                {
                    warningMessage = "Warning SMS sent";
                    Log.Information("Geofence warning SMS sent to renter {RenterId} for booking {BookingId}. Distance: {Distance}km", 
                        booking.RenterId, bookingId, distanceKm);
                }
                else
                {
                    Log.Warning("Failed to send geofence warning SMS to renter {RenterId} for booking {BookingId}", 
                        booking.RenterId, bookingId);
                }
            }
        }

        await _context.SaveChangesAsync();

        return (isWithin, distanceKm, warningMessage);
    }

    /// <summary>
    /// Monitors all active bookings and checks geofence violations
    /// This should be called periodically (e.g., every 5-10 minutes)
    /// </summary>
    public async Task MonitorActiveBookingsAsync()
    {
        var activeBookings = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .Where(b => b.BookingStatusId == 2) // Active bookings
            .ToListAsync();

        Log.Information("Monitoring {Count} active bookings for geofence violations", activeBookings.Count);

        foreach (var booking in activeBookings)
        {
            // Get the most recent location
            var latestLocation = await _context.LocationTracking
                .Where(lt => lt.BookingId == booking.BookingId)
                .OrderByDescending(t => t.TrackedAt)
                .FirstOrDefaultAsync();

            if (latestLocation == null)
            {
                // No location tracking yet - skip
                continue;
            }

            // Check if location is still valid (within last 30 minutes)
            var timeSinceLastUpdate = (DateTime.UtcNow - latestLocation.TrackedAt).TotalMinutes;
            if (timeSinceLastUpdate > 30)
            {
                // Location is stale - skip
                continue;
            }

            // If already outside geofence, check if we need to send another warning
            if (!latestLocation.IsWithinGeofence)
            {
                // Check if we've sent a warning recently
                var lastWarningTime = latestLocation.TrackedAt;
                var minutesSinceWarning = (DateTime.UtcNow - lastWarningTime).TotalMinutes;

                // Send warning every 15 minutes if still outside
                if (minutesSinceWarning >= 15 && !string.IsNullOrWhiteSpace(booking.Renter.Phone))
                {
                    var owner = booking.Bike.Owner;
                    var radius = owner.GeofenceRadiusKm ?? GetDefaultGeofenceRadius();
                    var distance = latestLocation.DistanceFromStoreKm ?? 0;
                    var message = $"⚠️ Geofence Alert: You are still {distance:F1}km away from {owner.StoreName ?? "the store location"}. " +
                                 $"Please return within {radius}km radius immediately.";

                    var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
                    if (smsSent)
                    {
                        Log.Information("Periodic geofence warning SMS sent to renter {RenterId} for booking {BookingId}. Distance: {Distance}km", 
                            booking.RenterId, booking.BookingId, distance);
                    }
                }
            }
        }
    }
}

