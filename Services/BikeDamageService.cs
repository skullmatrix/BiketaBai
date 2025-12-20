using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class BikeDamageService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public BikeDamageService(BiketaBaiDbContext context, NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool Success, int DamageId, string Message)> ReportDamageAsync(
        int bookingId,
        int ownerId,
        string damageDescription,
        decimal damageCost,
        string? damageDetails = null,
        string? damageImageUrl = null)
    {
        // Get booking to verify ownership
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return (false, 0, "Booking not found");

        // Verify the owner owns the bike
        if (booking.Bike.OwnerId != ownerId)
            return (false, 0, "You do not have permission to report damage for this booking");

        // Only allow damage reporting for completed bookings
        if (booking.BookingStatus != "Completed")
            return (false, 0, "You can only report damages for completed bookings");

        // Validate damage cost
        if (damageCost <= 0)
            return (false, 0, "Damage cost must be greater than zero");

        var damage = new BikeDamage
        {
            BookingId = bookingId,
            BikeId = booking.BikeId,
            OwnerId = ownerId,
            RenterId = booking.RenterId,
            DamageDescription = damageDescription,
            DamageDetails = damageDetails,
            DamageCost = damageCost,
            DamageImageUrl = damageImageUrl,
            DamageStatus = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BikeDamages.Add(damage);
        await _context.SaveChangesAsync();

        // Notify renter about the damage charge
        await _notificationService.CreateNotificationAsync(
            booking.RenterId,
            "Damage Charge Reported",
            $"The owner has reported damage to the bike from booking #{bookingId}. Damage cost: ₱{damageCost:F2}. Please review and pay the damage fee.",
            "Damage",
            $"/Renter/Damages/{damage.DamageId}"
        );

        // Notify admins
        var admins = await _context.Users
            .Where(u => u.IsAdmin)
            .ToListAsync();

        foreach (var admin in admins)
        {
            await _notificationService.CreateNotificationAsync(
                admin.UserId,
                "Damage Reported",
                $"Owner {booking.Bike.Owner.FullName} reported damage for booking #{bookingId}. Cost: ₱{damageCost:F2}",
                "Damage",
                $"/Admin/Damages/{damage.DamageId}"
            );
        }

        return (true, damage.DamageId, $"Damage reported successfully. Renter has been notified and must pay ₱{damageCost:F2}");
    }

    public async Task<List<BikeDamage>> GetDamagesForBookingAsync(int bookingId)
    {
        return await _context.BikeDamages
            .Include(d => d.Owner)
            .Include(d => d.Renter)
            .Include(d => d.Bike)
            .Where(d => d.BookingId == bookingId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<BikeDamage>> GetDamagesForRenterAsync(int renterId)
    {
        return await _context.BikeDamages
            .Include(d => d.Owner)
            .Include(d => d.Bike)
            .Include(d => d.Booking)
            .Where(d => d.RenterId == renterId && d.DamageStatus == "Pending")
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<BikeDamage>> GetDamagesForOwnerAsync(int ownerId)
    {
        return await _context.BikeDamages
            .Include(d => d.Renter)
            .Include(d => d.Bike)
            .Include(d => d.Booking)
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalPendingDamageCostForRenterAsync(int renterId)
    {
        return await _context.BikeDamages
            .Where(d => d.RenterId == renterId && d.DamageStatus == "Pending")
            .SumAsync(d => d.DamageCost);
    }

    public async Task<bool> MarkDamageAsPaidAsync(int damageId, int renterId, string? paymentNotes = null)
    {
        var damage = await _context.BikeDamages
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.DamageId == damageId && d.RenterId == renterId);

        if (damage == null)
            return false;

        if (damage.DamageStatus != "Pending")
            return false;

        damage.DamageStatus = "Paid";
        damage.PaidAt = DateTime.UtcNow;
        damage.PaymentNotes = paymentNotes;
        damage.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify owner
        await _notificationService.CreateNotificationAsync(
            damage.OwnerId,
            "Damage Fee Paid",
            $"Renter {damage.Renter.FullName} has paid the damage fee of ₱{damage.DamageCost:F2} for booking #{damage.BookingId}",
            "Payment",
            $"/Owner/Damages/{damage.DamageId}"
        );

        return true;
    }
}

