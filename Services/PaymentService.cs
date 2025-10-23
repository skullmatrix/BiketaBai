using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BiketaBai.Services;

public class PaymentService
{
    private readonly BiketaBaiDbContext _context;
    private readonly WalletService _walletService;
    private readonly NotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public PaymentService(BiketaBaiDbContext context, WalletService walletService, NotificationService notificationService, IConfiguration configuration)
    {
        _context = context;
        _walletService = walletService;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<(bool success, string message)> ProcessPaymentAsync(int bookingId, int paymentMethodId, decimal amount, string? transactionReference = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return (false, "Booking not found");

        // Create payment record
        var payment = new Payment
        {
            BookingId = bookingId,
            PaymentMethodId = paymentMethodId,
            Amount = amount,
            PaymentStatus = "Pending",
            TransactionReference = transactionReference ?? Guid.NewGuid().ToString(),
            PaymentDate = DateTime.UtcNow
        };

        // Process based on payment method
        bool paymentSuccess = false;
        string message = "";

        switch (paymentMethodId)
        {
            case 1: // Wallet
                paymentSuccess = await _walletService.DeductFromWalletAsync(
                    booking.RenterId,
                    amount,
                    3, // Rental Payment
                    $"Payment for booking #{bookingId}",
                    $"Booking-{bookingId}"
                );
                message = paymentSuccess ? "Payment successful via Wallet" : "Insufficient wallet balance";
                break;

            case 2: // GCash
            case 3: // QRPH
                // Simulate payment gateway - in real implementation, integrate with actual payment API
                paymentSuccess = true;
                message = "Payment successful via " + (paymentMethodId == 2 ? "GCash" : "QRPH");
                break;

            case 4: // Cash
                paymentSuccess = true;
                payment.PaymentStatus = "Pending"; // Will be confirmed when owner receives cash
                message = "Booking confirmed. Please pay cash to the owner";
                break;

            default:
                return (false, "Invalid payment method");
        }

        if (paymentSuccess)
        {
            payment.PaymentStatus = paymentMethodId == 4 ? "Pending" : "Completed";
            _context.Payments.Add(payment);
            
            // Update booking status
            booking.BookingStatusId = 2; // Active
            booking.UpdatedAt = DateTime.UtcNow;

            // Update bike status
            booking.Bike.AvailabilityStatusId = 2; // Rented
            booking.Bike.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send notifications
            await _notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Payment Successful",
                $"Your payment of ₱{amount:F2} for booking #{bookingId} has been processed",
                "Payment"
            );

            await _notificationService.CreateNotificationAsync(
                booking.Bike.OwnerId,
                "New Booking",
                $"Your bike {booking.Bike.Brand} {booking.Bike.Model} has been booked",
                "Booking"
            );

            return (true, message);
        }

        return (false, message);
    }

    public async Task<bool> ProcessRefundAsync(int bookingId, decimal refundAmount)
    {
        var booking = await _context.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null) return false;

        var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == "Completed");
        if (payment == null) return false;

        // Add refund to renter's wallet
        await _walletService.AddToWalletAsync(
            booking.RenterId,
            refundAmount,
            5, // Refund
            $"Refund for cancelled booking #{bookingId}",
            $"Booking-{bookingId}"
        );

        // Update payment record
        payment.RefundAmount = refundAmount;
        payment.RefundDate = DateTime.UtcNow;
        payment.PaymentStatus = "Refunded";

        await _context.SaveChangesAsync();

        await _notificationService.CreateNotificationAsync(
            booking.RenterId,
            "Refund Processed",
            $"₱{refundAmount:F2} has been refunded to your wallet for booking #{bookingId}",
            "Payment"
        );

        return true;
    }

    public async Task<bool> DistributeEarningsAsync(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null) return false;

        var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == "Completed");
        if (payment == null) return false;

        var serviceFeePercentage = _configuration.GetValue<decimal>("AppSettings:ServiceFeePercentage");
        var serviceFee = booking.TotalAmount * (serviceFeePercentage / 100);
        var ownerEarnings = booking.TotalAmount - serviceFee;

        // Add earnings to owner's wallet
        await _walletService.AddToWalletAsync(
            booking.Bike.OwnerId,
            ownerEarnings,
            4, // Rental Earnings
            $"Earnings from booking #{bookingId}",
            $"Booking-{bookingId}"
        );

        await _notificationService.CreateNotificationAsync(
            booking.Bike.OwnerId,
            "Earnings Received",
            $"₱{ownerEarnings:F2} has been added to your wallet from booking #{bookingId}",
            "Payment"
        );

        return true;
    }

    public async Task<List<Payment>> GetPaymentHistoryAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        return await _context.Payments
            .Include(p => p.Booking)
            .ThenInclude(b => b.Bike)
            .Include(p => p.PaymentMethod)
            .Where(p => p.Booking.RenterId == userId || p.Booking.Bike.OwnerId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}

