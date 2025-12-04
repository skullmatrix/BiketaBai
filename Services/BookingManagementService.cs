using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class BookingManagementService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<BookingManagementService> _logger;

    public BookingManagementService(
        BiketaBaiDbContext context,
        NotificationService notificationService,
        ILogger<BookingManagementService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Search and filter bikes with advanced criteria
    /// </summary>
    public async Task<List<Bike>> SearchBikesAsync(
        string? location = null,
        int? bikeTypeId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string sortBy = "newest")
    {
        var query = _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => b.AvailabilityStatus == "Available") // Only available bikes
            .AsQueryable();

        // Apply filters
        if (bikeTypeId.HasValue)
        {
            query = query.Where(b => b.BikeTypeId == bikeTypeId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(b => b.HourlyRate >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(b => b.HourlyRate <= maxPrice.Value);
        }

        // Apply sorting
        query = sortBy switch
        {
            "price-low" => query.OrderBy(b => b.HourlyRate),
            "price-high" => query.OrderByDescending(b => b.HourlyRate),
            "newest" => query.OrderByDescending(b => b.CreatedAt),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        return await query.ToListAsync();
    }

    /// <summary>
    /// Create a new booking request
    /// </summary>
    public async Task<(bool Success, int BookingId, string Message)> CreateBookingAsync(
        int renterId,
        int bikeId,
        DateTime startDate,
        DateTime endDate)
    {
        try
        {
            // Validate bike exists and is available
            var bike = await _context.Bikes
                .Include(b => b.Owner)
                .FirstOrDefaultAsync(b => b.BikeId == bikeId);

            if (bike == null)
            {
                return (false, 0, "Bike not found");
            }

            if (bike.AvailabilityStatus != "Available")
            {
                return (false, 0, "Bike is not available for booking");
            }

            // Validate dates
            if (startDate < DateTime.Now)
            {
                return (false, 0, "Start date cannot be in the past");
            }

            if (endDate <= startDate)
            {
                return (false, 0, "End date must be after start date");
            }

            // Check for overlapping bookings
            var hasConflict = await _context.Bookings
                .AnyAsync(b => b.BikeId == bikeId &&
                              (b.BookingStatus == "Pending" || b.BookingStatus == "Active") &&
                              ((startDate >= b.StartDate && startDate < b.EndDate) ||
                               (endDate > b.StartDate && endDate <= b.EndDate) ||
                               (startDate <= b.StartDate && endDate >= b.EndDate)));

            if (hasConflict)
            {
                return (false, 0, "Bike is already booked for the selected dates");
            }

            // Calculate total amount
            var hours = (decimal)(endDate - startDate).TotalHours;
            var days = hours / 24;
            
            decimal baseRate = 0;
            if (days >= 1)
            {
                var fullDays = (int)Math.Floor(days);
                var remainingHours = hours - (fullDays * 24);
                baseRate = (fullDays * bike.DailyRate) + (remainingHours * bike.HourlyRate);
            }
            else
            {
                baseRate = hours * bike.HourlyRate;
            }

            var serviceFee = baseRate * 0.10m; // 10% service fee
            var totalAmount = baseRate + serviceFee;

            // Create booking
            var booking = new Booking
            {
                BikeId = bikeId,
                RenterId = renterId,
                BookingStatus = "Pending",
                StartDate = startDate,
                EndDate = endDate,
                RentalHours = hours,
                BaseRate = baseRate,
                ServiceFee = serviceFee,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // Send notification to bike owner
            await _notificationService.CreateNotificationAsync(
                bike.OwnerId,
                "New Booking Request",
                $"You have a new rental request for {bike.Brand} {bike.Model}",
                "/Owner/RentalRequests"
            );

            // Send confirmation to renter
            await _notificationService.CreateNotificationAsync(
                renterId,
                "Booking Request Sent",
                $"Your booking request for {bike.Brand} {bike.Model} has been sent to the owner",
                $"/Renter/RentalHistory"
            );

            _logger.LogInformation($"Booking {booking.BookingId} created by renter {renterId} for bike {bikeId}");

            return (true, booking.BookingId, $"Booking request sent successfully! Total: ₱{totalAmount:N2}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return (false, 0, $"Error creating booking: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel a booking
    /// </summary>
    public async Task<(bool Success, string Message)> CancelBookingAsync(int bookingId, int userId)
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
                return (false, "Booking not found");
            }

            // Verify ownership
            if (booking.RenterId != userId)
            {
                return (false, "You don't have permission to cancel this booking");
            }

            // Check if can be cancelled
            if (booking.BookingStatus == "Completed")
            {
                return (false, "Cannot cancel completed bookings");
            }

            if (booking.BookingStatus == "Cancelled")
            {
                return (false, "Booking is already cancelled");
            }

            // Calculate refund based on cancellation policy
            var hoursUntilStart = (booking.StartDate - DateTime.Now).TotalHours;
            var refundAmount = booking.TotalAmount;
            var refundPercentage = 100;

            if (hoursUntilStart < 24)
            {
                // Less than 24 hours: 50% refund
                refundAmount = booking.TotalAmount * 0.50m;
                refundPercentage = 50;
            }

            // Update booking status to Cancelled
            booking.BookingStatus = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;

            // Note: Refunds will be processed through payment gateway or manual transfer
            // No wallet functionality - refunds go back to original payment method

            // If bike was set to rented, make it available again
            if (booking.Bike.AvailabilityStatus == "Rented")
            {
                booking.Bike.AvailabilityStatus = "Available";
                booking.Bike.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify owner
            await _notificationService.CreateNotificationAsync(
                booking.Bike.OwnerId,
                "Booking Cancelled",
                $"Booking for {booking.Bike.Brand} {booking.Bike.Model} has been cancelled by {booking.Renter.FullName}",
                "/Owner/RentalRequests"
            );

            // Notify renter
            await _notificationService.CreateNotificationAsync(
                userId,
                "Booking Cancelled",
                $"Your booking has been cancelled. Refund of ₱{refundAmount:N2} ({refundPercentage}%) will be processed to your payment method.",
                "/Renter/RentalHistory"
            );

            _logger.LogInformation($"Booking {bookingId} cancelled by user {userId} with {refundPercentage}% refund");

            return (true, $"Booking cancelled successfully! ₱{refundAmount:N2} ({refundPercentage}%) will be refunded to your payment method.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cancelling booking {bookingId}");
            return (false, $"Error cancelling booking: {ex.Message}");
        }
    }

    /// <summary>
    /// Get renter's booking history
    /// </summary>
    public async Task<List<Booking>> GetRenterBookingsAsync(int renterId, string? status = null)
    {
        var query = _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Bike.BikeType)
            .Include(b => b.Bike.Owner)
            .Where(b => b.RenterId == renterId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = status.ToLower() switch
            {
                "pending" => query.Where(b => b.BookingStatus == "Pending"),
                "active" => query.Where(b => b.BookingStatus == "Active"),
                "completed" => query.Where(b => b.BookingStatus == "Completed"),
                "cancelled" => query.Where(b => b.BookingStatus == "Cancelled"),
                _ => query
            };
        }

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Get booking statistics for renter
    /// </summary>
    public async Task<RenterBookingStatistics> GetRenterStatisticsAsync(int renterId)
    {
        var bookings = await _context.Bookings
            .Where(b => b.RenterId == renterId)
            .ToListAsync();

        return new RenterBookingStatistics
        {
            TotalBookings = bookings.Count,
            PendingBookings = bookings.Count(b => b.BookingStatus == "Pending"),
            ActiveBookings = bookings.Count(b => b.BookingStatus == "Active"),
            CompletedBookings = bookings.Count(b => b.BookingStatus == "Completed"),
            CancelledBookings = bookings.Count(b => b.BookingStatus == "Cancelled"),
            TotalSpent = bookings.Where(b => b.BookingStatus == "Completed").Sum(b => b.TotalAmount),
            TotalDistanceSaved = bookings.Where(b => b.DistanceSavedKm.HasValue).Sum(b => b.DistanceSavedKm ?? 0)
        };
    }

    /// <summary>
    /// Get bike details for booking
    /// </summary>
    public async Task<Bike?> GetBikeForBookingAsync(int bikeId)
    {
        return await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Include(b => b.Ratings)
                .ThenInclude(r => r.Rater)
            .FirstOrDefaultAsync(b => b.BikeId == bikeId);
    }

    /// <summary>
    /// Check if bike is available for specific dates
    /// </summary>
    public async Task<bool> IsBikeAvailableAsync(int bikeId, DateTime startDate, DateTime endDate)
    {
        var bike = await _context.Bikes.FindAsync(bikeId);
        if (bike == null || bike.AvailabilityStatus != "Available")
        {
            return false;
        }

        var hasConflict = await _context.Bookings
            .AnyAsync(b => b.BikeId == bikeId &&
                          (b.BookingStatus == "Pending" || b.BookingStatus == "Active") &&
                          ((startDate >= b.StartDate && startDate < b.EndDate) ||
                           (endDate > b.StartDate && endDate <= b.EndDate) ||
                           (startDate <= b.StartDate && endDate >= b.EndDate)));

        return !hasConflict;
    }

    /// <summary>
    /// Calculate booking price
    /// </summary>
    public async Task<BookingPriceCalculation> CalculateBookingPriceAsync(int bikeId, DateTime startDate, DateTime endDate)
    {
        var bike = await _context.Bikes.FindAsync(bikeId);
        if (bike == null)
        {
            return new BookingPriceCalculation();
        }

        var hours = (decimal)(endDate - startDate).TotalHours;
        var days = hours / 24;

        decimal baseAmount = 0;
        if (days >= 1)
        {
            var fullDays = (int)Math.Floor(days);
            var remainingHours = hours - (fullDays * 24);
            baseAmount = (fullDays * bike.DailyRate) + (remainingHours * bike.HourlyRate);
        }
        else
        {
            baseAmount = hours * bike.HourlyRate;
        }

        var serviceFee = baseAmount * 0.10m;
        var totalAmount = baseAmount + serviceFee;

        return new BookingPriceCalculation
        {
            BaseAmount = baseAmount,
            ServiceFee = serviceFee,
            TotalAmount = totalAmount,
            DurationHours = hours,
            DurationDays = days
        };
    }

    /// <summary>
    /// Get popular bikes (most booked)
    /// </summary>
    public async Task<List<Bike>> GetPopularBikesAsync(int count = 10)
    {
        var popularBikeIds = await _context.Bookings
            .Where(b => b.BookingStatus == "Completed")
            .GroupBy(b => b.BikeId)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync();

        return await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => popularBikeIds.Contains(b.BikeId) && b.AvailabilityStatus == "Available")
            .ToListAsync();
    }

    /// <summary>
    /// Get bike rating average
    /// </summary>
    public async Task<double> GetBikeAverageRatingAsync(int bikeId)
    {
        var ratings = await _context.Ratings
            .Where(r => r.BikeId == bikeId)
            .Select(r => r.RatingValue)
            .ToListAsync();

        return ratings.Any() ? ratings.Average() : 0;
    }
}

/// <summary>
/// Renter booking statistics DTO
/// </summary>
public class RenterBookingStatistics
{
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalDistanceSaved { get; set; }
}

/// <summary>
/// Booking price calculation DTO
/// </summary>
public class BookingPriceCalculation
{
    public decimal BaseAmount { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DurationHours { get; set; }
    public decimal DurationDays { get; set; }
}

