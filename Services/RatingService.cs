using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class RatingService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public RatingService(BiketaBaiDbContext context, NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<bool> SubmitRatingAsync(int bookingId, int raterId, int ratedUserId, int? bikeId, int ratingValue, string? review, bool isRenterRatingOwner)
    {
        // Check if rating already exists
        var existingRating = await _context.Ratings
            .FirstOrDefaultAsync(r => r.BookingId == bookingId && r.RaterId == raterId);

        if (existingRating != null) return false; // Already rated

        var rating = new Rating
        {
            BookingId = bookingId,
            BikeId = bikeId,
            RaterId = raterId,
            RatedUserId = ratedUserId,
            RatingValue = ratingValue,
            Review = review,
            IsRenterRatingOwner = isRenterRatingOwner,
            CreatedAt = DateTime.UtcNow
        };

        _context.Ratings.Add(rating);
        await _context.SaveChangesAsync();

        // Notify the rated user
        var raterName = await _context.Users
            .Where(u => u.UserId == raterId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();

        await _notificationService.CreateNotificationAsync(
            ratedUserId,
            "New Rating Received",
            $"{raterName} rated you {ratingValue} stars",
            "Rating"
        );

        return true;
    }

    public async Task<double> GetAverageRatingForUserAsync(int userId, bool asOwner = true)
    {
        var ratings = await _context.Ratings
            .Where(r => r.RatedUserId == userId && r.IsRenterRatingOwner == asOwner)
            .Select(r => r.RatingValue)
            .ToListAsync();

        if (!ratings.Any()) return 0;
        return ratings.Average();
    }

    public async Task<double> GetAverageRatingForBikeAsync(int bikeId)
    {
        var ratings = await _context.Ratings
            .Where(r => r.BikeId == bikeId)
            .Select(r => r.RatingValue)
            .ToListAsync();

        if (!ratings.Any()) return 0;
        return ratings.Average();
    }

    public async Task<int> GetRatingCountForUserAsync(int userId, bool asOwner = true)
    {
        return await _context.Ratings
            .Where(r => r.RatedUserId == userId && r.IsRenterRatingOwner == asOwner)
            .CountAsync();
    }

    public async Task<int> GetRatingCountForBikeAsync(int bikeId)
    {
        return await _context.Ratings
            .Where(r => r.BikeId == bikeId)
            .CountAsync();
    }

    public async Task<List<Rating>> GetRatingsForBikeAsync(int bikeId, int pageNumber = 1, int pageSize = 10)
    {
        return await _context.Ratings
            .Include(r => r.Rater)
            .Where(r => r.BikeId == bikeId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Rating>> GetRatingsForUserAsync(int userId, bool asOwner, int pageNumber = 1, int pageSize = 10)
    {
        return await _context.Ratings
            .Include(r => r.Rater)
            .Include(r => r.Bike)
            .Where(r => r.RatedUserId == userId && r.IsRenterRatingOwner == asOwner)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<bool> HasRatedBookingAsync(int bookingId, int raterId)
    {
        return await _context.Ratings
            .AnyAsync(r => r.BookingId == bookingId && r.RaterId == raterId);
    }
}

