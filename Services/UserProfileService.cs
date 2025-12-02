using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class UserProfileService
{
    private readonly BiketaBaiDbContext _context;

    public UserProfileService(BiketaBaiDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get user by ID with all related data
    /// </summary>
    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    /// <summary>
    /// Update user profile information
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string phone, string address)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        // Validate inputs
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Full name is required");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return (false, "Phone number is required");
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return (false, "Address is required");
        }

        // Update user data
        user.FullName = fullName.Trim();
        user.Phone = phone.Trim();
        user.Address = address.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            return (true, "Profile updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating profile: {ex.Message}");
        }
    }

    /// <summary>
    /// Update user profile photo
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateProfilePhotoAsync(int userId, string photoUrl)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        user.ProfilePhotoUrl = photoUrl;
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            return (true, "Profile photo updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating profile photo: {ex.Message}");
        }
    }

    /// <summary>
    /// Update user ID document (for owner verification)
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateIdDocumentAsync(int userId, string documentUrl)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        if (!user.IsOwner)
        {
            return (false, "Only owners can upload ID documents");
        }

        user.IdDocumentUrl = documentUrl;
        user.VerificationStatus = "Pending";
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            return (true, "ID document uploaded successfully! Your verification is now pending.");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating ID document: {ex.Message}");
        }
    }

    /// <summary>
    /// Get user statistics based on role
    /// </summary>
    public async Task<UserStatistics> GetUserStatisticsAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
        {
            return new UserStatistics();
        }

        var stats = new UserStatistics
        {
            WalletBalance = user.Wallet?.Balance ?? 0
        };

        if (user.IsRenter)
        {
            var renterBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Where(b => b.RenterId == userId)
                .ToListAsync();

            stats.TotalRentals = renterBookings.Count;
            stats.CompletedRentals = renterBookings.Count(b => b.BookingStatus.StatusName == "Completed");
            stats.ActiveRentals = renterBookings.Count(b => 
                b.BookingStatus.StatusName == "Active" || 
                b.BookingStatus.StatusName == "Confirmed");
            stats.TotalSpent = renterBookings
                .Where(b => b.BookingStatus.StatusName == "Completed" || b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount);

            // Get renter ratings (owners rating this renter)
            var renterRatings = await _context.Ratings
                .Where(r => r.RatedUserId == userId && !r.IsRenterRatingOwner)
                .ToListAsync();
            
            stats.AverageRating = renterRatings.Any() ? renterRatings.Average(r => r.RatingValue) : 0;
            stats.TotalReviews = renterRatings.Count;
        }

        if (user.IsOwner)
        {
            var bikes = await _context.Bikes
                .Where(b => b.OwnerId == userId)
                .ToListAsync();

            stats.TotalBikes = bikes.Count;

            var ownerBookings = await _context.Bookings
                .Include(b => b.BookingStatus)
                .Include(b => b.Bike)
                .Where(b => b.Bike.OwnerId == userId)
                .ToListAsync();

            stats.TotalBookings = ownerBookings.Count;
            stats.CompletedBookings = ownerBookings.Count(b => b.BookingStatus.StatusName == "Completed");
            
            // 90% share to owner, 10% platform fee
            stats.TotalEarnings = ownerBookings
                .Where(b => b.BookingStatus.StatusName == "Completed" || b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount * 0.90m);

            stats.PendingPayout = ownerBookings
                .Where(b => b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount * 0.90m);

            // Get owner ratings (renters rating this owner)
            var ownerRatings = await _context.Ratings
                .Where(r => r.RatedUserId == userId && r.IsRenterRatingOwner)
                .ToListAsync();
            
            stats.AverageRating = ownerRatings.Any() ? ownerRatings.Average(r => r.RatingValue) : 0;
            stats.TotalReviews = ownerRatings.Count;
        }

        return stats;
    }

    /// <summary>
    /// Get recent bookings for a user
    /// </summary>
    public async Task<List<Booking>> GetRecentBookingsAsync(int userId, bool isOwner, int count = 5)
    {
        IQueryable<Booking> query = _context.Bookings
            .Include(b => b.BookingStatus)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Bike.BikeType)
            .Include(b => b.Renter);

        if (isOwner)
        {
            query = query.Where(b => b.Bike.OwnerId == userId);
        }
        else
        {
            query = query.Where(b => b.RenterId == userId);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Check if email exists (for email update validation)
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, int excludeUserId)
    {
        return await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower() && u.UserId != excludeUserId);
    }

    /// <summary>
    /// Save uploaded file and return the file path
    /// </summary>
    public async Task<(bool Success, string? FilePath, string Message)> SaveUploadedFileAsync(
        IFormFile file, 
        string uploadFolder, 
        string[] allowedExtensions,
        long maxFileSize = 5 * 1024 * 1024) // 5MB default
    {
        if (file == null || file.Length == 0)
        {
            return (false, null, "No file uploaded");
        }

        // Validate file size
        if (file.Length > maxFileSize)
        {
            return (false, null, $"File size exceeds the maximum allowed size of {maxFileSize / (1024 * 1024)}MB");
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return (false, null, $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
        }

        try
        {
            // Create unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsPath = Path.Combine("wwwroot", uploadFolder);
            
            // Ensure directory exists
            Directory.CreateDirectory(uploadsPath);

            var filePath = Path.Combine(uploadsPath, fileName);
            
            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return web-accessible path
            var webPath = $"/{uploadFolder}/{fileName}";
            return (true, webPath, "File uploaded successfully");
        }
        catch (Exception ex)
        {
            return (false, null, $"Error uploading file: {ex.Message}");
        }
    }
}

/// <summary>
/// User statistics data transfer object
/// </summary>
public class UserStatistics
{
    // Common stats
    public decimal WalletBalance { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }

    // Renter stats
    public int TotalRentals { get; set; }
    public int CompletedRentals { get; set; }
    public int ActiveRentals { get; set; }
    public decimal TotalSpent { get; set; }

    // Owner stats
    public int TotalBikes { get; set; }
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal PendingPayout { get; set; }
}

