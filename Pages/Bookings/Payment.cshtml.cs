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
    private readonly WalletService _walletService;

    public PaymentModel(BiketaBaiDbContext context, PaymentService paymentService, WalletService walletService)
    {
        _context = context;
        _paymentService = paymentService;
        _walletService = walletService;
    }

    public Booking? Booking { get; set; }
    public decimal WalletBalance { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public Payment? ExistingPayment { get; set; }
    public int? CurrentPaymentMethodId { get; set; }

    [BindProperty]
    public int PaymentMethodId { get; set; } = 1;

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

        WalletBalance = await _walletService.GetBalanceAsync(userId.Value);

        // Check for existing pending payment
        ExistingPayment = Booking.Payments
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault(p => p.PaymentStatus == "Pending" || p.PaymentStatus == "Failed");

        if (ExistingPayment != null)
        {
            CurrentPaymentMethodId = ExistingPayment.PaymentMethodId;
            PaymentMethodId = ExistingPayment.PaymentMethodId; // Pre-select current method
        }
        else
        {
            // Ensure default is set if no existing payment
            if (PaymentMethodId == 0)
            {
                PaymentMethodId = 1; // Default to Wallet
            }
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
                .ThenInclude(p => p.PaymentMethod)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        WalletBalance = await _walletService.GetBalanceAsync(userId.Value);

        // Check for existing pending payment
        ExistingPayment = Booking.Payments
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault(p => p.PaymentStatus == "Pending" || p.PaymentStatus == "Failed");

        // If user is changing payment method and there's a pending payment, cancel it
        if (ExistingPayment != null && ExistingPayment.PaymentMethodId != PaymentMethodId)
        {
            // Cancel/void the existing pending payment
            if (ExistingPayment.PaymentStatus == "Pending" && !string.IsNullOrEmpty(ExistingPayment.TransactionReference))
            {
                // For gateway payments, we can mark as cancelled
                // The gateway will handle expiry
                ExistingPayment.PaymentStatus = "Cancelled";
                ExistingPayment.Notes = $"Cancelled - user changed to payment method {PaymentMethodId}";
                await _context.SaveChangesAsync();
            }
        }

        // Handle different payment methods
        if (PaymentMethodId == 1) // Wallet
        {
            // Validate wallet balance
            if (WalletBalance < Booking.TotalAmount)
        {
            ErrorMessage = "Insufficient wallet balance. Please top up your wallet or choose another payment method.";
            return Page();
        }

            // Process wallet payment
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
        else if (PaymentMethodId == 4) // Cash
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

                // For card payments, redirect to payment gateway page
                if (PaymentMethodId == 6)
                {
                    TempData["PaymentIntentId"] = gatewayResult.paymentIntentId;
                    return RedirectToPage("/Bookings/PaymentGateway", new { bookingId = bookingId });
                }
                // For GCash/PayMaya/QRPH, redirect to Xendit invoice page
                else if (!string.IsNullOrEmpty(gatewayResult.redirectUrl))
                {
                    return Redirect(gatewayResult.redirectUrl);
                }
                else
                {
                    // Fallback: redirect to confirmation page
                    return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
                }
            }
            else
            {
                ErrorMessage = gatewayResult.message;
                return Page();
            }
        }
    }
}

