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
    private readonly NotificationService _notificationService;
    private readonly AddressValidationService _addressValidationService;
    private readonly IConfiguration _configuration;

    public GeofencingService(
        BiketaBaiDbContext context,
        SmsService smsService,
        NotificationService notificationService,
        AddressValidationService addressValidationService,
        IConfiguration configuration)
    {
        _context = context;
        _smsService = smsService;
        _notificationService = notificationService;
        _addressValidationService = addressValidationService;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the default geofence radius in kilometers
    /// </summary>
    public decimal GetDefaultGeofenceRadius()
    {
        return _configuration.GetValue<decimal>("AppSettings:GeofenceRadiusKm", 5.0m); // Default 5km
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
            // Check notifications to see if we've sent a geofence warning recently
            var recentWarning = await _context.Notifications
                .Where(n => n.UserId == booking.RenterId 
                    && n.NotificationType == "Geofence"
                    && n.Title.Contains("Geofence Alert")
                    && n.Message.Contains($"Booking #{bookingId}"))
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            var shouldSendWarning = recentWarning == null || 
                (DateTime.UtcNow - recentWarning.CreatedAt).TotalMinutes >= 15;

            if (shouldSendWarning && !string.IsNullOrWhiteSpace(booking.Renter.Phone))
            {
                var owner = booking.Bike.Owner;
                var radius = owner.GeofenceRadiusKm ?? GetDefaultGeofenceRadius();
                var message = $"⚠️ Geofence Alert: You are {distanceKm:F1}km away from {owner.StoreName ?? "the store location"}. " +
                             $"Please return within {radius}km radius. Current distance: {distanceKm:F1}km. - Bike Ta Bai";

                try
                {
                    var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
                    if (smsSent)
                    {
                        warningMessage = "Warning SMS sent";
                        
                        // Create notification record to track that SMS was sent
                        var notification = new Notification
                        {
                            UserId = booking.RenterId,
                            Title = "Geofence Alert",
                            Message = $"Geofence Alert: You are {distanceKm:F1}km away from {owner.StoreName ?? "the store location"}. Booking #{bookingId}",
                            NotificationType = "Geofence",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Notifications.Add(notification);
                        
                        Log.Information("Geofence warning SMS sent to renter {RenterId} for booking {BookingId}. Distance: {Distance}km, Phone: {Phone}", 
                            booking.RenterId, bookingId, distanceKm, booking.Renter.Phone);
                    }
                    else
                    {
                        Log.Warning("Failed to send geofence warning SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                            booking.RenterId, bookingId, booking.Renter.Phone);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception while sending geofence warning SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                        booking.RenterId, bookingId, booking.Renter.Phone);
                }
            }
        }
        else
        {
            // If back within geofence, log it (no SMS needed)
            Log.Information("Renter {RenterId} for booking {BookingId} is back within geofence. Distance: {Distance}km", 
                booking.RenterId, bookingId, distanceKm);
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
        try
        {
            var activeBookings = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.Owner)
                .Include(b => b.Renter)
                .Where(b => b.BookingStatusId == 2) // Active bookings
                .ToListAsync();

            Log.Information("Monitoring {Count} active bookings for geofence violations and reminders", activeBookings.Count);
            
            if (activeBookings.Count == 0)
            {
                Log.Debug("No active bookings to monitor");
                return;
            }

            foreach (var booking in activeBookings)
            {
            // Check for overdue bookings and send SMS
            await CheckAndSendOverdueNotificationAsync(booking);

            // Check for 10-minute reminder
            await CheckAndSendReturnReminderAsync(booking);

            // Get the most recent location
            var latestLocation = await _context.LocationTracking
                .Where(lt => lt.BookingId == booking.BookingId)
                .OrderByDescending(t => t.TrackedAt)
                .FirstOrDefaultAsync();

            if (latestLocation == null)
            {
                // No location tracking yet - skip geofence check
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
                // Check if we've already sent a warning recently (within last 15 minutes)
                var recentWarning = await _context.Notifications
                    .Where(n => n.UserId == booking.RenterId 
                        && n.NotificationType == "Geofence"
                        && n.Title.Contains("Geofence Alert")
                        && n.Message.Contains($"Booking #{booking.BookingId}"))
                    .OrderByDescending(n => n.CreatedAt)
                    .FirstOrDefaultAsync();

                var shouldSend = recentWarning == null || 
                    (DateTime.UtcNow - recentWarning.CreatedAt).TotalMinutes >= 15;

                // Send warning every 15 minutes if still outside
                if (shouldSend && !string.IsNullOrWhiteSpace(booking.Renter.Phone))
                {
                    var owner = booking.Bike.Owner;
                    var radius = owner.GeofenceRadiusKm ?? GetDefaultGeofenceRadius();
                    var distance = latestLocation.DistanceFromStoreKm ?? 0;
                    var message = $"⚠️ Geofence Alert: You are still {distance:F1}km away from {owner.StoreName ?? "the store location"}. " +
                                 $"Please return within {radius}km radius immediately. - Bike Ta Bai";

                    try
                    {
                        var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
                        if (smsSent)
                        {
                            // Create notification record
                            var notification = new Notification
                            {
                                UserId = booking.RenterId,
                                Title = "Geofence Alert",
                                Message = $"Geofence Alert: You are still {distance:F1}km away from {owner.StoreName ?? "the store location"}. Booking #{booking.BookingId}",
                                NotificationType = "Geofence",
                                IsRead = false,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.Notifications.Add(notification);
                            await _context.SaveChangesAsync();
                            
                            Log.Information("Periodic geofence warning SMS sent to renter {RenterId} for booking {BookingId}. Distance: {Distance}km, Phone: {Phone}", 
                                booking.RenterId, booking.BookingId, distance, booking.Renter.Phone);
                        }
                        else
                        {
                            Log.Warning("Failed to send periodic geofence warning SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                                booking.RenterId, booking.BookingId, booking.Renter.Phone);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Exception while sending periodic geofence warning SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                            booking.RenterId, booking.BookingId, booking.Renter.Phone);
                    }
                }
            }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in MonitorActiveBookingsAsync: {ErrorMessage}", ex.Message);
            throw; // Re-throw to let background service handle retry
        }
    }

    /// <summary>
    /// Checks if booking is within 10 minutes of ending and sends reminder SMS
    /// </summary>
    private async Task CheckAndSendReturnReminderAsync(Booking booking)
    {
        if (string.IsNullOrWhiteSpace(booking.Renter.Phone))
        {
            return; // No phone number, skip
        }

        var now = DateTime.UtcNow;
        var timeRemaining = (booking.EndDate - now).TotalMinutes;

        // Check if booking is within 10 minutes of ending (between 10 and 0 minutes)
        // We check between 10 and 8 minutes to ensure we catch it and avoid duplicates
        if (timeRemaining > 10 || timeRemaining < 8)
        {
            return; // Too early or too late (already sent or expired)
        }

        // Check if we've already sent a reminder for this booking to avoid duplicates
        var recentReminder = await _context.Notifications
            .Where(n => n.UserId == booking.RenterId 
                && n.NotificationType == "Reminder"
                && n.Title.Contains("Return Reminder")
                && n.Message.Contains($"Booking #{booking.BookingId}"))
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentReminder != null)
        {
            return; // Already sent reminder for this booking
        }

        // Format return time (convert UTC to local time if needed, or use UTC)
        var returnTime = booking.EndDate.ToString("MMM dd, yyyy hh:mm tt");
        var minutesLeft = Math.Ceiling(timeRemaining);

        // Send reminder SMS
        var message = $"⏰ Return Reminder: You have {minutesLeft} minute(s) left. Please return the bike by {returnTime} to avoid penalties. Thank you! - Bike Ta Bai";
        
        try
        {
            var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
            
            if (smsSent)
            {
                // Create notification record
                var notification = new Notification
                {
                    UserId = booking.RenterId,
                    Title = "Return Reminder",
                    Message = $"Return reminder sent for Booking #{booking.BookingId}. Return by {returnTime}.",
                    NotificationType = "Reminder",
                    IsRead = false,
                    ActionUrl = $"/Bookings/Details/{booking.BookingId}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                Log.Information("Return reminder SMS sent to renter {RenterId} for booking {BookingId}. Time remaining: {Minutes} minutes, Phone: {Phone}", 
                    booking.RenterId, booking.BookingId, minutesLeft, booking.Renter.Phone);
            }
            else
            {
                Log.Warning("Failed to send return reminder SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                    booking.RenterId, booking.BookingId, booking.Renter.Phone);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception while sending return reminder SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                booking.RenterId, booking.BookingId, booking.Renter.Phone);
        }
    }

    /// <summary>
    /// Checks if booking is overdue and sends SMS notification to renter
    /// </summary>
    private async Task CheckAndSendOverdueNotificationAsync(Booking booking)
    {
        if (string.IsNullOrWhiteSpace(booking.Renter.Phone))
        {
            return; // No phone number, skip
        }

        var now = DateTime.UtcNow;
        
        // Check if booking is overdue (EndDate has passed)
        if (booking.EndDate >= now)
        {
            return; // Not overdue yet
        }

        var overdueMinutes = (now - booking.EndDate).TotalMinutes;

        // Check if we've already sent an overdue notification recently (within last 30 minutes)
        var recentOverdueNotification = await _context.Notifications
            .Where(n => n.UserId == booking.RenterId 
                && n.NotificationType == "Overdue"
                && n.Title.Contains("Overdue Return")
                && n.Message.Contains($"Booking #{booking.BookingId}"))
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOverdueNotification != null)
        {
            var minutesSinceNotification = (now - recentOverdueNotification.CreatedAt).TotalMinutes;
            if (minutesSinceNotification < 30)
            {
                return; // Already sent notification recently
            }
        }

        // Format return time
        var returnTime = booking.EndDate.ToString("MMM dd, yyyy hh:mm tt");
        var overdueHours = Math.Floor(overdueMinutes / 60);
        var overdueMins = Math.Floor(overdueMinutes % 60);

        // Send overdue SMS
        var message = $"⚠️ OVERDUE: Your bike rental (Booking #{booking.BookingId}) was due back on {returnTime}. " +
                     $"You are {overdueHours}h {overdueMins}m overdue. Please return the bike(s) immediately to avoid additional penalties. - Bike Ta Bai";
        
        try
        {
            var smsSent = await _smsService.SendSmsAsync(booking.Renter.Phone, message);
            
            if (smsSent)
            {
                // Create notification record
                var notification = new Notification
                {
                    UserId = booking.RenterId,
                    Title = "Overdue Return",
                    Message = $"Your bike rental (Booking #{booking.BookingId}) is overdue. Please return immediately.",
                    NotificationType = "Overdue",
                    IsRead = false,
                    ActionUrl = $"/Bookings/Details/{booking.BookingId}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // Also notify the owner about overdue return
                await _notificationService.CreateNotificationAsync(
                    booking.Bike.OwnerId,
                    "Bike Return Overdue",
                    $"Booking #{booking.BookingId} for {booking.Bike.Brand} {booking.Bike.Model} is overdue. Renter: {booking.Renter.FullName}. Overdue by {overdueHours}h {overdueMins}m.",
                    "Overdue",
                    $"/Owner/MyBikes"
                );

                Log.Information("Overdue notification SMS sent to renter {RenterId} for booking {BookingId}. Overdue by {Minutes} minutes, Phone: {Phone}", 
                    booking.RenterId, booking.BookingId, overdueMinutes, booking.Renter.Phone);
            }
            else
            {
                Log.Warning("Failed to send overdue notification SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                    booking.RenterId, booking.BookingId, booking.Renter.Phone);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception while sending overdue notification SMS to renter {RenterId} for booking {BookingId}. Phone: {Phone}", 
                booking.RenterId, booking.BookingId, booking.Renter.Phone);
        }
    }
}

