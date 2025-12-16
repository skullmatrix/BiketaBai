using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Bookings;

public class PaymentModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly PaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public PaymentModel(BiketaBaiDbContext context, PaymentService paymentService, IConfiguration configuration)
    {
        _context = context;
        _paymentService = paymentService;
        _configuration = configuration;
    }

    public Booking? Booking { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public Payment? ExistingPayment { get; set; }
    public int? CurrentPaymentMethodId { get; set; }

    [BindProperty]
    public int PaymentMethodId { get; set; } = 2; // Default to GCash (Wallet removed)

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Check for existing pending, failed, or cancelled payment (that can be resumed)
        ExistingPayment = Booking.Payments
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault(p => p.PaymentStatus == "Pending" || 
                                p.PaymentStatus == "Failed" || 
                                (p.PaymentStatus == "Cancelled" && !string.IsNullOrEmpty(p.TransactionReference)));

        if (ExistingPayment != null)
        {
            // Map payment method string back to ID for UI
            CurrentPaymentMethodId = MapPaymentMethodToId(ExistingPayment.PaymentMethod);
            PaymentMethodId = CurrentPaymentMethodId ?? 2; // Default to GCash
        }
        else
        {
            // Ensure default is set if no existing payment
            if (PaymentMethodId == 0)
            {
                PaymentMethodId = 2; // Default to GCash (Wallet removed)
            }
        }

        // If location permission is already granted, show success message
        if (Booking.LocationPermissionGranted)
        {
            SuccessMessage = "Location permission granted. You can proceed with payment.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Validate location permission is granted before allowing payment
        if (!Booking.LocationPermissionGranted)
        {
            ErrorMessage = "Location permission is required to proceed with payment. Please enable location access first.";
            
            // Check for existing pending, failed, or cancelled payment
            ExistingPayment = Booking.Payments
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefault(p => p.PaymentStatus == "Pending" || 
                                    p.PaymentStatus == "Failed" || 
                                    (p.PaymentStatus == "Cancelled" && !string.IsNullOrEmpty(p.TransactionReference)));
            
            if (ExistingPayment != null)
            {
                CurrentPaymentMethodId = MapPaymentMethodToId(ExistingPayment.PaymentMethod);
                PaymentMethodId = CurrentPaymentMethodId ?? 2;
            }
            
            return Page();
        }

        // Check for existing pending payment
        ExistingPayment = Booking.Payments
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault(p => p.PaymentStatus == "Pending" || p.PaymentStatus == "Failed");

        // If user is changing payment method and there's a pending payment, cancel it
        if (ExistingPayment != null)
        {
            var existingPaymentMethodId = MapPaymentMethodToId(ExistingPayment.PaymentMethod);
            if (existingPaymentMethodId.HasValue && existingPaymentMethodId.Value != PaymentMethodId)
            {
                // Cancel/void the existing pending payment
                if (ExistingPayment.PaymentStatus == "Pending" && !string.IsNullOrEmpty(ExistingPayment.TransactionReference))
                {
                    ExistingPayment.PaymentStatus = "Cancelled";
                    ExistingPayment.Notes = $"Cancelled - user changed payment method";
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Handle different payment methods
        if (PaymentMethodId == 4) // Cash
        {
            // Process cash payment
        var result = await _paymentService.ProcessPaymentAsync(
            bookingId,
            PaymentMethodId,
            Booking.TotalAmount
        );

        if (result.success)
        {
            return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
        }
        else
        {
            ErrorMessage = result.message;
            return Page();
            }
        }
        else // GCash, PayMaya, QRPH, or Card - Gateway payments
        {
            // Create payment intent for gateway payment
            var gatewayResult = await _paymentService.CreateGatewayPaymentAsync(
                bookingId,
                PaymentMethodId,
                Booking.TotalAmount
            );

            if (gatewayResult.success && !string.IsNullOrEmpty(gatewayResult.paymentIntentId))
            {
                // Store payment intent ID in TempData for confirmation
                TempData["PaymentIntentId"] = gatewayResult.paymentIntentId;
                TempData["BookingId"] = bookingId.ToString();

                // For all gateway payments (GCash, PayMaya, GrabPay, Card), redirect to payment gateway page
                // PayMongo requires frontend integration for all payment methods
                TempData["PaymentIntentId"] = gatewayResult.paymentIntentId;
                TempData["ClientKey"] = gatewayResult.clientKey; // Public key for PayMongo JS SDK
                TempData["PaymentMethodId"] = PaymentMethodId.ToString();
                return RedirectToPage("/Bookings/PaymentGateway", new { 
                    bookingId = bookingId, 
                    paymentIntentId = gatewayResult.paymentIntentId,
                    paymentMethodId = PaymentMethodId
                });
            }
            else
            {
                ErrorMessage = gatewayResult.message;
                return Page();
            }
        }
    }

    public async Task<IActionResult> OnPostContinuePaymentAsync(int bookingId, string paymentIntentId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Validate location permission
        if (!Booking.LocationPermissionGranted)
        {
            ErrorMessage = "Location permission is required to proceed with payment. Please enable location access first.";
            ExistingPayment = Booking.Payments
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefault(p => p.TransactionReference == paymentIntentId);
            if (ExistingPayment != null)
            {
                CurrentPaymentMethodId = MapPaymentMethodToId(ExistingPayment.PaymentMethod);
                PaymentMethodId = CurrentPaymentMethodId ?? 2;
            }
            return Page();
        }

        // Find the payment with this transaction reference
        var payment = Booking.Payments
            .FirstOrDefault(p => p.TransactionReference == paymentIntentId);

        if (payment == null)
        {
            ErrorMessage = "Payment not found. Please start a new payment.";
            return Page();
        }

        // Map payment method to ID
        var paymentMethodId = MapPaymentMethodToId(payment.PaymentMethod);
        if (!paymentMethodId.HasValue)
        {
            ErrorMessage = "Could not determine payment method. Please start a new payment.";
            return Page();
        }

        // Update payment status back to Pending if it was Cancelled
        if (payment.PaymentStatus == "Cancelled")
        {
            payment.PaymentStatus = "Pending";
            payment.Notes = "Resumed payment";
            await _context.SaveChangesAsync();
        }

        // Redirect to payment gateway with existing payment intent
        TempData["PaymentIntentId"] = paymentIntentId;
        TempData["ClientKey"] = _configuration["AppSettings:PayMongoPublicKey"] 
                             ?? Environment.GetEnvironmentVariable("AppSettings__PayMongoPublicKey")
                             ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoPublicKey")
                             ?? Environment.GetEnvironmentVariable("PayMongoPublicKey")
                             ?? "";
        TempData["PaymentMethodId"] = paymentMethodId.Value.ToString();

        return RedirectToPage("/Bookings/PaymentGateway", new 
        { 
            bookingId = bookingId, 
            paymentIntentId = paymentIntentId,
            paymentMethodId = paymentMethodId.Value
        });
    }

    private int? MapPaymentMethodToId(string paymentMethod)
    {
        return paymentMethod switch
        {
            "GCash" => 2,
            "QRPH" => 3,
            "Cash" => 4,
            "PayMaya" => 5,
            "Credit/Debit Card" => 6,
            _ => null
        };
    }
}

