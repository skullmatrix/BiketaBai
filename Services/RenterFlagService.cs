using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

    public async Task<bool> FlagRenterAsync(int bookingId, int ownerId, string flagReason, string? flagDescription = null, decimal? damageCost = null, string? damagePhotoUrl = null)
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
        {
            Log.Warning("FlagRenterAsync: Booking {BookingId} not found", bookingId);
            return false;
        }

        // Verify the owner owns the bike
        if (booking.Bike == null || booking.Bike.OwnerId != ownerId)
        {
            Log.Warning("FlagRenterAsync: Owner {OwnerId} does not own bike for booking {BookingId}", ownerId, bookingId);
            return false;
        }

        // Only allow flagging completed bookings
        if (booking.BookingStatus != "Completed")
        {
            Log.Warning("FlagRenterAsync: Booking {BookingId} is not completed (status: {Status})", bookingId, booking.BookingStatus);
            return false;
        }

        if (booking.Renter == null || booking.RenterId <= 0)
        {
            Log.Warning("FlagRenterAsync: Booking {BookingId} has invalid RenterId: {RenterId}", bookingId, booking.RenterId);
            return false;
        }

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

        // If flagging for damage, create a BikeDamage record
        if (flagReason == "Damage" && damageCost.HasValue && damageCost.Value > 0)
        {
            try
            {
                // Validate that RenterId is set
                if (booking.RenterId <= 0)
                {
                    throw new InvalidOperationException($"Invalid RenterId for booking {bookingId}. Cannot create damage record.");
                }

                var bikeDamage = new BikeDamage
                {
                    BookingId = bookingId,
                    BikeId = booking.BikeId,
                    OwnerId = ownerId,
                    RenterId = booking.RenterId,
                    DamageDescription = flagDescription ?? "Damage reported via flag",
                    DamageDetails = flagDescription,
                    DamageCost = damageCost.Value,
                    DamageImageUrl = damagePhotoUrl,
                    DamageStatus = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.BikeDamages.Add(bikeDamage);
                await _context.SaveChangesAsync();

                // Notify renter about the damage charge with the damage ID in the URL
                if (booking.Renter != null && booking.RenterId > 0)
                {
                    await _notificationService.CreateNotificationAsync(
                        booking.RenterId,
                        "Damage Charge Reported",
                        $"The owner has reported damage to the bike from booking #{bookingId}. Damage cost: ₱{damageCost.Value:F2}. Please review and pay the damage fee.",
                        "Damage",
                        $"/Renter/Damages"
                    );
                }
            }
            catch (Exception ex)
            {
                // Log error for debugging
                Log.Error(ex, "Error creating BikeDamage record for booking {BookingId} during flagging. RenterId: {RenterId}, OwnerId: {OwnerId}. Flag was still created.", 
                    bookingId, booking.RenterId, ownerId);
                // Don't fail the flag operation - flag was already saved successfully
                // But this means the damage record might not exist, which could cause the "damage record not found" error
            }
        }

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


