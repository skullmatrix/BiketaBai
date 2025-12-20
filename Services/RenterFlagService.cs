using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class RenterFlagService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public RenterFlagService(BiketaBaiDbContext context, NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<bool> FlagRenterAsync(int bookingId, int ownerId, string flagReason, string? flagDescription = null)
    {
        // Check if flag already exists for this booking
        var existingFlag = await _context.RenterFlags
            .FirstOrDefaultAsync(f => f.BookingId == bookingId && f.OwnerId == ownerId);

        if (existingFlag != null)
            return false; // Already flagged

        // Get booking to verify ownership and get renter ID
        var booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return false;

        // Verify the owner owns the bike
        if (booking.Bike == null || booking.Bike.OwnerId != ownerId)
            return false;

        // Only allow flagging completed bookings
        if (booking.BookingStatus != "Completed")
            return false;

        if (booking.Renter == null)
            return false;

        var flag = new RenterFlag
        {
            BookingId = bookingId,
            OwnerId = ownerId,
            RenterId = booking.RenterId,
            FlagReason = flagReason,
            FlagDescription = flagDescription,
            CreatedAt = DateTime.UtcNow
        };

        _context.RenterFlags.Add(flag);
        await _context.SaveChangesAsync();

        // Notify admin about the flag
        try
        {
            var admins = await _context.Users
                .Where(u => u.IsAdmin)
                .ToListAsync();

            var ownerName = booking.Bike?.Owner?.FullName ?? "Unknown Owner";
            var renterName = booking.Renter?.FullName ?? "Unknown Renter";

            foreach (var admin in admins)
            {
                await _notificationService.CreateNotificationAsync(
                    admin.UserId,
                    "Renter Flagged",
                    $"Owner {ownerName} flagged renter {renterName} for booking #{bookingId}. Reason: {flagReason}",
                    "Flag",
                    $"/Admin/Flags/{flag.FlagId}"
                );
            }
        }
        catch
        {
            // Log error but don't fail the flag operation
            // Flag was already saved successfully
        }

        return true;
    }

    public async Task<List<RenterFlag>> GetFlagsForRenterAsync(int renterId)
    {
        return await _context.RenterFlags
            .Include(f => f.Owner)
            .Include(f => f.Booking)
                .ThenInclude(b => b.Bike)
            .Where(f => f.RenterId == renterId && !f.IsResolved)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> HasFlaggedBookingAsync(int bookingId, int ownerId)
    {
        return await _context.RenterFlags
            .AnyAsync(f => f.BookingId == bookingId && f.OwnerId == ownerId);
    }
}


