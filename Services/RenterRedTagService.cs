using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class RenterRedTagService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public RenterRedTagService(BiketaBaiDbContext context, NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool Success, int RedTagId, string Message)> RedTagRenterAsync(
        int renterId,
        int ownerId,
        string redTagReason,
        string? redTagDescription = null,
        int? bookingId = null)
    {
        // Check if renter is already red-tagged (active)
        var existingRedTag = await _context.RenterRedTags
            .FirstOrDefaultAsync(t => t.RenterId == renterId && t.IsActive);

        if (existingRedTag != null)
            return (false, 0, "This renter is already red-tagged");

        // Verify owner exists (for reference, but admin can red tag on behalf of owner)
        var owner = await _context.Users.FindAsync(ownerId);
        if (owner == null)
            return (false, 0, "Invalid owner reference");

        // Verify renter exists
        var renter = await _context.Users.FindAsync(renterId);
        if (renter == null)
            return (false, 0, "Renter not found");

        // If booking ID is provided, verify it belongs to this owner and renter
        if (bookingId.HasValue)
        {
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId.Value);

            if (booking == null)
                return (false, 0, "Booking not found");

            if (booking.Bike.OwnerId != ownerId || booking.RenterId != renterId)
                return (false, 0, "Invalid booking for this owner and renter");
        }

        var redTag = new RenterRedTag
        {
            RenterId = renterId,
            OwnerId = ownerId,
            BookingId = bookingId,
            RedTagReason = redTagReason,
            RedTagDescription = redTagDescription,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.RenterRedTags.Add(redTag);
        await _context.SaveChangesAsync();

        // Notify renter
        if (renter != null)
        {
            await _notificationService.CreateNotificationAsync(
                renterId,
                "Red Tagged - Account Restricted",
                $"You have been RED TAGGED by {owner?.FullName ?? "an owner"}. Reason: {redTagReason}. Your account may be restricted from making new bookings. Please contact support.",
                "RedTag",
                "/Dashboard/Renter"
            );
        }

        // Notify all admins
        var admins = await _context.Users
            .Where(u => u.IsAdmin)
            .ToListAsync();

        foreach (var admin in admins)
        {
            await _notificationService.CreateNotificationAsync(
                admin.UserId,
                "Renter Red Tagged",
                $"Owner {owner?.FullName ?? "Unknown"} has RED TAGGED renter {renter?.FullName ?? "Unknown"}. Reason: {redTagReason}",
                "RedTag",
                $"/Admin/RedTags/{redTag.RedTagId}"
            );
        }

        return (true, redTag.RedTagId, $"Renter {renter.FullName} has been RED TAGGED. Administrators have been notified.");
    }

    public async Task<bool> IsRenterRedTaggedAsync(int renterId)
    {
        return await _context.RenterRedTags
            .AnyAsync(t => t.RenterId == renterId && t.IsActive);
    }

    public async Task<List<RenterRedTag>> GetActiveRedTagsForRenterAsync(int renterId)
    {
        return await _context.RenterRedTags
            .Include(t => t.Owner)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Bike)
            .Where(t => t.RenterId == renterId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<RenterRedTag>> GetRedTagsForOwnerAsync(int ownerId)
    {
        return await _context.RenterRedTags
            .Include(t => t.Renter)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Bike)
            .Where(t => t.OwnerId == ownerId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ResolveRedTagAsync(int redTagId, int adminId, string? resolutionNotes = null)
    {
        var redTag = await _context.RenterRedTags
            .Include(t => t.Renter)
            .FirstOrDefaultAsync(t => t.RedTagId == redTagId);

        if (redTag == null || !redTag.IsActive)
            return false;

        redTag.IsActive = false;
        redTag.ResolvedAt = DateTime.UtcNow;
        redTag.ResolvedBy = adminId;
        redTag.ResolutionNotes = resolutionNotes;

        await _context.SaveChangesAsync();

        // Notify renter
        await _notificationService.CreateNotificationAsync(
            redTag.RenterId,
            "Red Tag Resolved",
            $"Your red tag has been resolved by an administrator. You can now make bookings again.",
            "RedTag",
            "/Dashboard/Renter"
        );

        return true;
    }
}

