using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BiketaBai.Services;

public class PaymentService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly PaymentGatewayService _paymentGatewayService;
    private readonly IConfiguration _configuration;

    public PaymentService(
        BiketaBaiDbContext context, 
        NotificationService notificationService, 
        PaymentGatewayService paymentGatewayService,
        IConfiguration configuration)
    {
        _context = context;
        _notificationService = notificationService;
        _paymentGatewayService = paymentGatewayService;
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

        // Map payment method ID to string
        string paymentMethodString = paymentMethodId switch
        {
            2 => "GCash",
            3 => "QRPH",
            4 => "Cash",
            5 => "PayMaya",
            6 => "Credit/Debit Card",
            _ => "Unknown"
        };

        // Create payment record
        var payment = new Payment
        {
            BookingId = bookingId,
            PaymentMethod = paymentMethodString,
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
            case 2: // GCash
            case 5: // PayMaya
            case 3: // QRPH
            case 6: // Credit/Debit Card
                // These will be handled via payment gateway redirect
                // For now, return payment intent creation result
                var paymentMethodType = paymentMethodId switch
                {
                    2 => "gcash",
                    5 => "paymaya",
                    3 => "qrph",
                    6 => "card",
                    _ => "gcash"
                };

                var paymentIntent = await _paymentGatewayService.CreatePaymentIntentAsync(
                    amount,
                    "PHP",
                    paymentMethodType,
                    $"Booking payment for booking #{bookingId}",
                    new Dictionary<string, string>
                    {
                        { "booking_id", bookingId.ToString() },
                        { "renter_id", booking.RenterId.ToString() }
                    }
                );

                if (paymentIntent.Success && !string.IsNullOrEmpty(paymentIntent.PaymentIntentId))
                {
                    payment.TransactionReference = paymentIntent.PaymentIntentId;
                    payment.PaymentStatus = "Pending"; // Will be updated via webhook
                paymentSuccess = true;
                    message = $"Payment intent created. Please complete payment via {GetPaymentMethodName(paymentMethodId)}";
                }
                else
                {
                    paymentSuccess = false;
                    message = paymentIntent.ErrorMessage ?? "Failed to create payment intent";
                }
                break;

            case 4: // Cash
                paymentSuccess = true;
                payment.PaymentStatus = "Pending"; // Will be confirmed when owner verifies cash payment
                message = "Booking created. Please pay cash to the owner. The rental time will start once the owner verifies your payment.";
                
                // For cash payments, don't set StartDate/EndDate yet - they will be set when owner verifies
                // The RentalHours is already stored in the booking, so we can calculate dates later
                // Keep the existing StartDate/EndDate as placeholder (they'll be updated on verification)
                break;

            default:
                return (false, "Invalid payment method");
        }

        if (paymentSuccess)
        {
            // Validate location permission is granted before processing payment
            if (!booking.LocationPermissionGranted)
            {
                return (false, "Location permission is required to proceed with payment. Please enable location access first.");
            }

            payment.PaymentStatus = paymentMethodId == 4 ? "Pending" : "Completed";
            _context.Payments.Add(payment);
            
            // For cash payments, keep booking as Pending until owner verifies
            // For other payments, activate booking immediately
            if (paymentMethodId == 4) // Cash
            {
                // Booking stays Pending until owner verifies payment
                booking.BookingStatus = "Pending";
                // Bike status remains Available until payment is verified
                // Don't change bike status here
            }
            else
            {
                // For non-cash payments, activate booking immediately
                booking.BookingStatus = "Active";
                // Update bike status to Rented
                booking.Bike.AvailabilityStatus = "Rented";
                booking.Bike.UpdatedAt = DateTime.UtcNow;
            }
            
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send notifications
            if (paymentMethodId == 4) // Cash
            {
                await _notificationService.CreateNotificationAsync(
                    booking.RenterId,
                    "Booking Created - Cash Payment",
                    $"Your booking #{bookingId} has been created. Please pay ₱{amount:F2} cash to the owner. The rental time will start once the owner verifies your payment.",
                    "Payment",
                    $"/Bookings/Details/{bookingId}"
                );

                await _notificationService.CreateNotificationAsync(
                    booking.Bike.OwnerId,
                    "New Booking - Cash Payment Required",
                    $"Booking #{bookingId.ToString("D6")} - Renter: {booking.Renter.FullName} | Bike: {booking.Bike.Brand} {booking.Bike.Model} | Amount: ₱{amount:F2} | Please verify cash payment to activate. Location permission has been granted.",
                    "Booking",
                    $"/Owner/RentalRequests"
                );

                // Send real-time SignalR event for cash payment request
                await _notificationService.SendCashPaymentRequestAsync(
                    booking.Bike.OwnerId,
                    bookingId,
                    booking.Renter.FullName,
                    $"{booking.Bike.Brand} {booking.Bike.Model}",
                    amount
                );
            }
            else
            {
                // For non-cash payments, booking is immediately active - send notification with geofencing link
                var endDatePHT = TimeZoneHelper.FormatPhilippineTime(booking.EndDate);
                await _notificationService.CreateNotificationAsync(
                    booking.RenterId,
                    "Payment Successful - Rental Active",
                    $"Your payment of ₱{amount:F2} for booking #{bookingId} has been processed. Your rental is now active and will end on {endDatePHT}. Please start location tracking for geofencing.",
                    "Payment",
                    $"/Bookings/TrackLocation/{bookingId}"
                );

                await _notificationService.CreateNotificationAsync(
                    booking.Bike.OwnerId,
                    "New Booking",
                    $"Your bike {booking.Bike.Brand} {booking.Bike.Model} has been booked. Location permission has been granted.",
                    "Booking",
                    $"/Owner/RentalRequests"
                );
            }

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

        // Update payment record for refund
        // Note: Refunds will be processed through payment gateway or manual transfer
        payment.RefundAmount = refundAmount;
        payment.RefundDate = DateTime.UtcNow;
        payment.PaymentStatus = "Refunded";

        await _context.SaveChangesAsync();

        await _notificationService.CreateNotificationAsync(
            booking.RenterId,
            "Refund Processed",
            $"₱{refundAmount:F2} refund has been processed for booking #{bookingId}. Please check your payment method for the refund.",
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

        // Note: Earnings distribution - owners receive payments directly through payment gateway
        // Platform fee is handled at payment processing time
        // No wallet needed - earnings go directly to owner's payment method
        
        await _notificationService.CreateNotificationAsync(
            booking.Bike.OwnerId,
            "Payment Received",
            $"₱{ownerEarnings:F2} has been received from booking #{bookingId}.",
            "Payment"
        );

        return true;
    }

    public async Task<List<Payment>> GetPaymentHistoryAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        return await _context.Payments
            .Include(p => p.Booking)
            .ThenInclude(b => b.Bike)
            .Where(p => p.Booking.RenterId == userId || p.Booking.Bike.OwnerId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Gets payment method name by ID
    /// </summary>
    private string GetPaymentMethodName(int paymentMethodId)
    {
        return paymentMethodId switch
        {
            2 => "GCash",
            3 => "QRPH",
            4 => "Cash",
            5 => "PayMaya",
            6 => "Credit/Debit Card",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Creates a payment intent for gateway payments (GCash, PayMaya, QRPH, Cards)
    /// Returns the payment intent ID and redirect URL
    /// </summary>
    public async Task<(bool success, string? paymentIntentId, string? redirectUrl, string? clientKey, string message)> CreateGatewayPaymentAsync(
        int bookingId, 
        int paymentMethodId, 
        decimal amount)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return (false, null, null, null, "Booking not found");

            var paymentMethodType = paymentMethodId switch
            {
                2 => "gcash",
                5 => "paymaya",
                3 => "qrph",
                6 => "card",
                _ => "gcash"
            };

            // Get renter email for PayMongo payment intent
            var renter = await _context.Users.FindAsync(booking.RenterId);
            var renterEmail = renter?.Email ?? "";
            var renterName = renter?.FullName ?? "Customer";

            var paymentIntent = await _paymentGatewayService.CreatePaymentIntentAsync(
                amount,
                "PHP",
                paymentMethodType,
                $"Booking payment for {booking.Bike.Brand} {booking.Bike.Model}",
                new Dictionary<string, string>
                {
                    { "booking_id", bookingId.ToString() },
                    { "renter_id", booking.RenterId.ToString() },
                    { "customer_email", renterEmail },
                    { "customer_name", renterName }
                }
            );

            if (paymentIntent.Success && !string.IsNullOrEmpty(paymentIntent.PaymentIntentId))
            {
                // Map payment method ID to string
                string paymentMethodString = paymentMethodId switch
                {
                    2 => "GCash",
                    3 => "QRPH",
                    5 => "PayMaya",
                    6 => "Credit/Debit Card",
                    _ => "GCash"
                };

                // Create pending payment record
                var payment = new Payment
                {
                    BookingId = bookingId,
                    PaymentMethod = paymentMethodString,
                    Amount = amount,
                    PaymentStatus = "Pending",
                    TransactionReference = paymentIntent.PaymentIntentId,
                    PaymentDate = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // PayMongo returns public key in ClientKey for frontend integration
                // For e-wallet payments, redirect to PayMongo checkout
                // Get base URL from configuration or environment variables (support multiple formats)
                var baseUrl = _configuration["AppSettings:BaseUrl"] 
                           ?? Environment.GetEnvironmentVariable("AppSettings__BaseUrl")
                           ?? Environment.GetEnvironmentVariable("AppSettings:BaseUrl")
                           ?? Environment.GetEnvironmentVariable("BaseUrl")
                           ?? "http://localhost:5000";
                var redirectUrl = $"{baseUrl}/Bookings/PaymentGateway?bookingId={bookingId}&paymentIntentId={paymentIntent.PaymentIntentId}";

                return (true, paymentIntent.PaymentIntentId, redirectUrl, paymentIntent.ClientKey, "Payment intent created successfully");
            }

            return (false, null, null, null, paymentIntent.ErrorMessage ?? "Failed to create payment intent");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating gateway payment");
            return (false, null, null, null, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Confirms payment after gateway redirect or webhook
    /// </summary>
    public async Task<(bool success, string message)> ConfirmGatewayPaymentAsync(string paymentIntentId)
    {
        try
        {
            var paymentStatus = await _paymentGatewayService.GetPaymentIntentStatusAsync(paymentIntentId);

            if (!paymentStatus.Success)
                return (false, "Failed to retrieve payment status");

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .ThenInclude(b => b.Bike)
                .FirstOrDefaultAsync(p => p.TransactionReference == paymentIntentId);

            if (payment == null)
                return (false, "Payment record not found");

            // PayMongo statuses: awaiting_payment_method, awaiting_next_action, processing, succeeded, payment_failed
            if (paymentStatus.Status == "succeeded")
            {
                payment.PaymentStatus = "Completed";
                payment.PaymentDate = DateTime.UtcNow;

                // Update booking status
                payment.Booking.BookingStatus = "Active";
                payment.Booking.UpdatedAt = DateTime.UtcNow;

                // Update bike status
                payment.Booking.Bike.AvailabilityStatus = "Rented";
                payment.Booking.Bike.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notifications with geofencing link and condition photo reminder
                var endDatePHT = TimeZoneHelper.FormatPhilippineTime(payment.Booking.EndDate);
                await _notificationService.CreateNotificationAsync(
                    payment.Booking.RenterId,
                    "Payment Successful - Rental Active",
                    $"Your payment of ₱{payment.Amount:F2} has been processed. Your rental is now active and will end on {endDatePHT}. Please upload bike condition photos and start location tracking.",
                    "Payment",
                    $"/Bookings/Confirmation/{payment.Booking.BookingId}"
                );

                await _notificationService.CreateNotificationAsync(
                    payment.Booking.Bike.OwnerId,
                    "New Booking",
                    $"Your bike {payment.Booking.Bike.Brand} {payment.Booking.Bike.Model} has been booked",
                    "Booking",
                    $"/Owner/RentalRequests"
                );

                return (true, "Payment confirmed successfully");
            }
            else if (paymentStatus.Status == "awaiting_payment_method" || paymentStatus.Status == "awaiting_next_action" || paymentStatus.Status == "processing")
            {
                return (false, "Payment is still pending. Please complete the payment.");
            }
            else
            {
                payment.PaymentStatus = "Failed";
                await _context.SaveChangesAsync();
                return (false, "Payment failed or was cancelled");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error confirming gateway payment");
            return (false, $"Error: {ex.Message}");
        }
    }
}

