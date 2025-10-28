using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class BikeManagementService
{
    private readonly BiketaBaiDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<BikeManagementService> _logger;

    public BikeManagementService(
        BiketaBaiDbContext context,
        IWebHostEnvironment environment,
        ILogger<BikeManagementService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Get bike by ID with all related data
    /// </summary>
    public async Task<Bike?> GetBikeByIdAsync(int bikeId, int ownerId)
    {
        return await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.AvailabilityStatus)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .FirstOrDefaultAsync(b => b.BikeId == bikeId && b.OwnerId == ownerId);
    }

    /// <summary>
    /// Get all bikes for an owner
    /// </summary>
    public async Task<List<Bike>> GetOwnerBikesAsync(int ownerId)
    {
        return await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.AvailabilityStatus)
            .Include(b => b.BikeImages)
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get bike statistics for owner dashboard
    /// </summary>
    public async Task<BikeStatistics> GetBikeStatisticsAsync(int ownerId)
    {
        var bikes = await GetOwnerBikesAsync(ownerId);
        
        var stats = new BikeStatistics
        {
            TotalBikes = bikes.Count,
            AvailableBikes = bikes.Count(b => b.AvailabilityStatusId == 1),
            RentedBikes = bikes.Count(b => b.AvailabilityStatusId == 2),
            MaintenanceBikes = bikes.Count(b => b.AvailabilityStatusId == 3),
            InactiveBikes = bikes.Count(b => b.AvailabilityStatusId == 4)
        };

        // Get bookings stats
        var bikeIds = bikes.Select(b => b.BikeId).ToList();
        
        var bookings = await _context.Bookings
            .Where(b => bikeIds.Contains(b.BikeId))
            .ToListAsync();

        stats.TotalBookings = bookings.Count;
        stats.ActiveBookings = bookings.Count(b => b.BookingStatusId == 2);
        stats.PendingRequests = bookings.Count(b => b.BookingStatusId == 1);
        
        stats.TotalEarnings = bookings
            .Where(b => b.BookingStatusId == 3) // Completed
            .Sum(b => b.TotalAmount * 0.90m); // 90% owner share

        return stats;
    }

    /// <summary>
    /// Get average rating for a bike
    /// </summary>
    public async Task<double> GetBikeAverageRatingAsync(int bikeId)
    {
        var ratings = await _context.Ratings
            .Where(r => r.BikeId == bikeId)
            .Select(r => r.RatingValue)
            .ToListAsync();

        return ratings.Any() ? ratings.Average() : 0;
    }

    /// <summary>
    /// Get active bookings for a bike
    /// </summary>
    public async Task<int> GetActiveBikeBookingsCountAsync(int bikeId)
    {
        return await _context.Bookings
            .CountAsync(b => b.BikeId == bikeId && b.BookingStatusId == 2);
    }

    /// <summary>
    /// Create a new bike listing
    /// </summary>
    public async Task<(bool Success, int BikeId, string Message)> CreateBikeAsync(
        int ownerId,
        int bikeTypeId,
        string brand,
        string model,
        decimal mileage,
        string location,
        decimal? latitude,
        decimal? longitude,
        string? description,
        decimal hourlyRate,
        decimal dailyRate,
        List<IFormFile>? images)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                return (false, 0, "Brand and model are required");
            }

            if (hourlyRate <= 0 || dailyRate <= 0)
            {
                return (false, 0, "Rates must be greater than zero");
            }

            // Create bike
            var bike = new Bike
            {
                OwnerId = ownerId,
                BikeTypeId = bikeTypeId,
                Brand = brand.Trim(),
                Model = model.Trim(),
                Mileage = mileage,
                Location = location.Trim(),
                Latitude = latitude,
                Longitude = longitude,
                Description = description?.Trim(),
                HourlyRate = hourlyRate,
                DailyRate = dailyRate,
                AvailabilityStatusId = 1, // Available
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Bikes.Add(bike);
            await _context.SaveChangesAsync();

            // Handle image uploads
            if (images != null && images.Any())
            {
                await SaveBikeImagesAsync(bike.BikeId, images);
            }

            _logger.LogInformation($"Bike {bike.BikeId} created by owner {ownerId}");
            return (true, bike.BikeId, "Bike listed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bike");
            return (false, 0, $"Error creating bike: {ex.Message}");
        }
    }

    /// <summary>
    /// Update bike listing
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateBikeAsync(
        int bikeId,
        int ownerId,
        int bikeTypeId,
        string brand,
        string model,
        decimal mileage,
        string location,
        decimal? latitude,
        decimal? longitude,
        string? description,
        decimal hourlyRate,
        decimal dailyRate,
        List<IFormFile>? newImages = null)
    {
        try
        {
            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.BikeId == bikeId && b.OwnerId == ownerId);

            if (bike == null)
            {
                return (false, "Bike not found or you don't have permission to edit it");
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
            {
                return (false, "Brand and model are required");
            }

            if (hourlyRate <= 0 || dailyRate <= 0)
            {
                return (false, "Rates must be greater than zero");
            }

            // Update bike details
            bike.BikeTypeId = bikeTypeId;
            bike.Brand = brand.Trim();
            bike.Model = model.Trim();
            bike.Mileage = mileage;
            bike.Location = location.Trim();
            bike.Latitude = latitude;
            bike.Longitude = longitude;
            bike.Description = description?.Trim();
            bike.HourlyRate = hourlyRate;
            bike.DailyRate = dailyRate;
            bike.UpdatedAt = DateTime.UtcNow;

            // Add new images if provided
            if (newImages != null && newImages.Any())
            {
                await SaveBikeImagesAsync(bikeId, newImages);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Bike {bikeId} updated by owner {ownerId}");
            return (true, "Bike updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating bike {bikeId}");
            return (false, $"Error updating bike: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete bike listing
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteBikeAsync(int bikeId, int ownerId)
    {
        try
        {
            var bike = await _context.Bikes
                .Include(b => b.BikeImages)
                .FirstOrDefaultAsync(b => b.BikeId == bikeId && b.OwnerId == ownerId);

            if (bike == null)
            {
                return (false, "Bike not found or you don't have permission to delete it");
            }

            // Check for active bookings
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => b.BikeId == bikeId && (b.BookingStatusId == 1 || b.BookingStatusId == 2));

            if (hasActiveBookings)
            {
                return (false, "Cannot delete bike with pending or active bookings");
            }

            // Delete bike images from file system
            foreach (var image in bike.BikeImages)
            {
                DeleteBikeImage(image.ImageUrl);
            }

            _context.Bikes.Remove(bike);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Bike {bikeId} deleted by owner {ownerId}");
            return (true, "Bike deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting bike {bikeId}");
            return (false, $"Error deleting bike: {ex.Message}");
        }
    }

    /// <summary>
    /// Set bike availability status
    /// </summary>
    public async Task<(bool Success, string Message)> SetBikeAvailabilityAsync(
        int bikeId,
        int ownerId,
        int availabilityStatusId)
    {
        try
        {
            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.BikeId == bikeId && b.OwnerId == ownerId);

            if (bike == null)
            {
                return (false, "Bike not found or you don't have permission to modify it");
            }

            // Validate status ID
            if (availabilityStatusId < 1 || availabilityStatusId > 4)
            {
                return (false, "Invalid availability status");
            }

            // Check if bike is currently rented
            var isRented = await _context.Bookings
                .AnyAsync(b => b.BikeId == bikeId && b.BookingStatusId == 2); // Active

            if (isRented && availabilityStatusId == 1)
            {
                return (false, "Cannot set bike to available while it has active bookings");
            }

            bike.AvailabilityStatusId = availabilityStatusId;
            bike.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var statusName = availabilityStatusId switch
            {
                1 => "Available",
                2 => "Rented",
                3 => "Maintenance",
                4 => "Inactive",
                _ => "Unknown"
            };

            _logger.LogInformation($"Bike {bikeId} availability changed to {statusName} by owner {ownerId}");
            return (true, $"Bike availability set to {statusName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting bike {bikeId} availability");
            return (false, $"Error updating availability: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a specific bike image
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteBikeImageAsync(int imageId, int ownerId)
    {
        try
        {
            var image = await _context.BikeImages
                .Include(i => i.Bike)
                .FirstOrDefaultAsync(i => i.ImageId == imageId && i.Bike.OwnerId == ownerId);

            if (image == null)
            {
                return (false, "Image not found or you don't have permission to delete it");
            }

            // Delete from file system
            DeleteBikeImage(image.ImageUrl);

            // Delete from database
            _context.BikeImages.Remove(image);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Image {imageId} deleted by owner {ownerId}");
            return (true, "Image deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting image {imageId}");
            return (false, $"Error deleting image: {ex.Message}");
        }
    }

    /// <summary>
    /// Get pending booking requests for owner's bikes
    /// </summary>
    public async Task<List<Booking>> GetPendingBookingRequestsAsync(int ownerId)
    {
        var bikeIds = await _context.Bikes
            .Where(b => b.OwnerId == ownerId)
            .Select(b => b.BikeId)
            .ToListAsync();

        return await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Renter)
            .Include(b => b.BookingStatus)
            .Where(b => bikeIds.Contains(b.BikeId) && b.BookingStatusId == 1) // Pending
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all booking requests for owner's bikes
    /// </summary>
    public async Task<List<Booking>> GetAllBookingRequestsAsync(int ownerId, int statusId = 0)
    {
        var bikeIds = await _context.Bikes
            .Where(b => b.OwnerId == ownerId)
            .Select(b => b.BikeId)
            .ToListAsync();

        var query = _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Renter)
            .Include(b => b.BookingStatus)
            .Where(b => bikeIds.Contains(b.BikeId));

        if (statusId > 0)
        {
            query = query.Where(b => b.BookingStatusId == statusId);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Save bike images
    /// </summary>
    private async Task SaveBikeImagesAsync(int bikeId, List<IFormFile> images)
    {
        var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "bikes");
        Directory.CreateDirectory(uploadPath);

        // Get existing images count to determine if new image should be primary
        var existingImagesCount = await _context.BikeImages
            .CountAsync(i => i.BikeId == bikeId);

        int imageIndex = 0;
        foreach (var image in images.Take(5)) // Limit to 5 images
        {
            if (image.Length > 0)
            {
                // Validate file size (max 5MB)
                if (image.Length > 5 * 1024 * 1024)
                {
                    _logger.LogWarning($"Image too large: {image.Length} bytes");
                    continue;
                }

                // Validate file type
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(extension))
                {
                    _logger.LogWarning($"Invalid image type: {extension}");
                    continue;
                }

                var fileName = $"{bikeId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var bikeImage = new BikeImage
                {
                    BikeId = bikeId,
                    ImageUrl = $"/uploads/bikes/{fileName}",
                    IsPrimary = existingImagesCount == 0 && imageIndex == 0, // First image is primary only if no existing images
                    UploadedAt = DateTime.UtcNow
                };

                _context.BikeImages.Add(bikeImage);
                imageIndex++;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete bike image from file system
    /// </summary>
    private void DeleteBikeImage(string imageUrl)
    {
        try
        {
            var filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Could not delete image file: {imageUrl}");
        }
    }
}

/// <summary>
/// Bike statistics data transfer object
/// </summary>
public class BikeStatistics
{
    public int TotalBikes { get; set; }
    public int AvailableBikes { get; set; }
    public int RentedBikes { get; set; }
    public int MaintenanceBikes { get; set; }
    public int InactiveBikes { get; set; }
    public int TotalBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int PendingRequests { get; set; }
    public decimal TotalEarnings { get; set; }
}

