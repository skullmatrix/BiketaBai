using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class BookingManagementService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly PointsService _pointsService;
    private readonly ILogger<BookingManagementService> _logger;

    public BookingManagementService(
        BiketaBaiDbContext context,
        NotificationService notificationService,
        PointsService pointsService,
        ILogger<BookingManagementService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _pointsService = pointsService;
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
            .Include(b => b.AvailabilityStatus)
            .Where(b => b.AvailabilityStatusId == 1) // Only available bikes
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(b => b.Location.Contains(location));
        }

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
        DateTime endDate,
        string pickupLocation,
        string returnLocation,
        decimal? distanceSavedKm = null)
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

            if (bike.AvailabilityStatusId != 1)
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
                              (b.BookingStatusId == 1 || b.BookingStatusId == 2) && // Pending or Active
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

            var serviceFee = baseAmount * 0.10m; // 10% service fee
            var totalAmount = baseAmount + serviceFee;

            // Check wallet balance
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == renterId);
            if (wallet == null || wallet.Balance < totalAmount)
            {
                return (false, 0, $"Insufficient wallet balance. You need ₱{totalAmount:N2}");
            }

            // Deduct from wallet
            wallet.Balance -= totalAmount;
            wallet.UpdatedAt = DateTime.UtcNow;

            // Create wallet transaction
            var transaction = new CreditTransaction
            {
                WalletId = wallet.WalletId,
                Amount = -totalAmount,
                TransactionTypeId = 2, // Debit
                Description = $"Booking payment for {bike.Brand} {bike.Model}",
                CreatedAt = DateTime.UtcNow
            };
            _context.CreditTransactions.Add(transaction);

            // Create booking
            var booking = new Booking
            {
                BikeId = bikeId,
                RenterId = renterId,
                BookingStatusId = 1, // Pending
                StartDate = startDate,
                EndDate = endDate,
                TotalAmount = totalAmount,
                PickupLocation = pickupLocation,
                ReturnLocation = returnLocation,
                DistanceSavedKm = distanceSavedKm,
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
                .Include(b => b.BookingStatus)
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
            if (booking.BookingStatusId == 3) // Completed
            {
                return (false, "Cannot cancel completed bookings");
            }

            if (booking.BookingStatusId == 4) // Already cancelled
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
            booking.BookingStatusId = 4; // Cancelled
            booking.UpdatedAt = DateTime.UtcNow;

            // Refund to wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet != null)
            {
                wallet.Balance += refundAmount;
                wallet.UpdatedAt = DateTime.UtcNow;

                // Create refund transaction
                var refundTransaction = new CreditTransaction
                {
                    WalletId = wallet.WalletId,
                    Amount = refundAmount,
                    TransactionTypeId = 1, // Credit
                    Description = $"Refund for cancelled booking #{bookingId} ({refundPercentage}%)",
                    CreatedAt = DateTime.UtcNow
                };
                _context.CreditTransactions.Add(refundTransaction);
            }

            // If bike was set to rented, make it available again
            if (booking.Bike.AvailabilityStatusId == 2)
            {
                booking.Bike.AvailabilityStatusId = 1; // Available
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
                $"Your booking has been cancelled. Refund of ₱{refundAmount:N2} ({refundPercentage}%) has been processed",
                "/Wallet/Index"
            );

            _logger.LogInformation($"Booking {bookingId} cancelled by user {userId} with {refundPercentage}% refund");

            return (true, $"Booking cancelled successfully! ₱{refundAmount:N2} ({refundPercentage}%) refunded to your wallet");
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
            .Include(b => b.BookingStatus)
            .Where(b => b.RenterId == renterId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = status.ToLower() switch
            {
                "pending" => query.Where(b => b.BookingStatusId == 1),
                "active" => query.Where(b => b.BookingStatusId == 2),
                "completed" => query.Where(b => b.BookingStatusId == 3),
                "cancelled" => query.Where(b => b.BookingStatusId == 4),
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
            PendingBookings = bookings.Count(b => b.BookingStatusId == 1),
            ActiveBookings = bookings.Count(b => b.BookingStatusId == 2),
            CompletedBookings = bookings.Count(b => b.BookingStatusId == 3),
            CancelledBookings = bookings.Count(b => b.BookingStatusId == 4),
            TotalSpent = bookings.Where(b => b.BookingStatusId == 3).Sum(b => b.TotalAmount),
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
            .Include(b => b.AvailabilityStatus)
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
        if (bike == null || bike.AvailabilityStatusId != 1)
        {
            return false;
        }

        var hasConflict = await _context.Bookings
            .AnyAsync(b => b.BikeId == bikeId &&
                          (b.BookingStatusId == 1 || b.BookingStatusId == 2) &&
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
            .Where(b => b.BookingStatusId == 3) // Completed
            .GroupBy(b => b.BikeId)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync();

        return await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => popularBikeIds.Contains(b.BikeId) && b.AvailabilityStatusId == 1)
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

