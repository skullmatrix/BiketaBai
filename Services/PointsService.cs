using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BiketaBai.Services;

public class PointsService
{
    private readonly BiketaBaiDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly NotificationService _notificationService;

    public PointsService(BiketaBaiDbContext context, IConfiguration configuration, NotificationService notificationService)
    {
        _context = context;
        _configuration = configuration;
        _notificationService = notificationService;
    }

    public async Task<Points> GetOrCreatePointsAsync(int userId)
    {
        var points = await _context.Points
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (points == null)
        {
            points = new Points
            {
                UserId = userId,
                TotalPoints = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Points.Add(points);
            await _context.SaveChangesAsync();
        }

        return points;
    }

    public async Task<int> GetPointsBalanceAsync(int userId)
    {
        var points = await GetOrCreatePointsAsync(userId);
        return points.TotalPoints;
    }

    public async Task<bool> AwardPointsAsync(int userId, int pointsToAdd, string reason, string? referenceId = null)
    {
        if (pointsToAdd <= 0) return false;

        var points = await GetOrCreatePointsAsync(userId);
        var pointsBefore = points.TotalPoints;
        points.TotalPoints += pointsToAdd;
        points.UpdatedAt = DateTime.UtcNow;

        var history = new PointsHistory
        {
            PointsId = points.PointsId,
            PointsChange = pointsToAdd,
            PointsBefore = pointsBefore,
            PointsAfter = points.TotalPoints,
            Reason = reason,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PointsHistory.Add(history);
        await _context.SaveChangesAsync();

        // Send notification
        await _notificationService.CreateNotificationAsync(
            userId,
            "Points Earned!",
            $"You earned {pointsToAdd} points for {reason}",
            "Points"
        );

        return true;
    }

    public async Task<bool> RedeemPointsAsync(int userId, int pointsToRedeem, string reason, string? referenceId = null)
    {
        if (pointsToRedeem <= 0) return false;

        var points = await GetOrCreatePointsAsync(userId);
        if (points.TotalPoints < pointsToRedeem) return false;

        var pointsBefore = points.TotalPoints;
        points.TotalPoints -= pointsToRedeem;
        points.UpdatedAt = DateTime.UtcNow;

        var history = new PointsHistory
        {
            PointsId = points.PointsId,
            PointsChange = -pointsToRedeem,
            PointsBefore = pointsBefore,
            PointsAfter = points.TotalPoints,
            Reason = reason,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.PointsHistory.Add(history);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task CalculateAndAwardBookingPointsAsync(int userId, Booking booking, bool onTime, int? ratingReceived = null)
    {
        var pointsRules = _configuration.GetSection("PointsRules");

        // On-time return bonus
        if (onTime)
        {
            var onTimePoints = pointsRules.GetValue<int>("OnTimeReturn");
            await AwardPointsAsync(userId, onTimePoints, "On-time return", $"Booking-{booking.BookingId}");
        }

        // Eco-commuter bonus (based on distance saved)
        if (booking.DistanceSavedKm.HasValue && booking.DistanceSavedKm.Value > 0)
        {
            var perKmPoints = pointsRules.GetValue<int>("EcoCommuteBonusPerKm");
            var ecoPoints = (int)(booking.DistanceSavedKm.Value * perKmPoints);
            await AwardPointsAsync(userId, ecoPoints, $"Eco-commute: {booking.DistanceSavedKm.Value:F2} km saved", $"Booking-{booking.BookingId}");
        }

        // Check if first rental
        var previousBookings = await _context.Bookings
            .Where(b => b.RenterId == userId && b.BookingStatusId == 3) // Completed
            .CountAsync();

        if (previousBookings == 1) // This is the first completed booking
        {
            var firstRentalPoints = pointsRules.GetValue<int>("FirstRental");
            await AwardPointsAsync(userId, firstRentalPoints, "First rental", $"Booking-{booking.BookingId}");
        }

        // Long-term rental bonus (>7 days)
        var rentalDays = (booking.EndDate - booking.StartDate).TotalDays;
        if (rentalDays > 7)
        {
            var longTermPoints = pointsRules.GetValue<int>("LongTermRental");
            await AwardPointsAsync(userId, longTermPoints, "Long-term rental (7+ days)", $"Booking-{booking.BookingId}");
        }

        // Highly rated bonus
        if (ratingReceived.HasValue && ratingReceived.Value == 5)
        {
            var highlyRatedPoints = pointsRules.GetValue<int>("HighlyRated");
            await AwardPointsAsync(userId, highlyRatedPoints, "Received 5-star rating", $"Booking-{booking.BookingId}");
        }
    }

    public async Task<decimal> ConvertPointsToCredits(int points)
    {
        var conversionRate = _configuration.GetValue<decimal>("AppSettings:PointsConversionRate");
        return points * conversionRate;
    }

    public async Task<List<PointsHistory>> GetPointsHistoryAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        var points = await GetOrCreatePointsAsync(userId);
        
        return await _context.PointsHistory
            .Where(ph => ph.PointsId == points.PointsId)
            .OrderByDescending(ph => ph.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}

