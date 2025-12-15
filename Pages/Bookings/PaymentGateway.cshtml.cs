using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Bookings;

public class PaymentGatewayModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly PaymentService _paymentService;
    private readonly PaymentGatewayService _paymentGatewayService;

    public PaymentGatewayModel(
        BiketaBaiDbContext context, 
        PaymentService paymentService,
        PaymentGatewayService paymentGatewayService)
    {
        _context = context;
        _paymentService = paymentService;
        _paymentGatewayService = paymentGatewayService;
    }

    public Booking? Booking { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? ClientKey { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int? PaymentMethodId { get; set; } // To determine if it's e-wallet or card

    [BindProperty]
    public CardPaymentModel CardPayment { get; set; } = new();

    public class CardPaymentModel
    {
        public string CardholderName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int bookingId, string? paymentIntentId, string? action, int? paymentMethodId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
            .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Handle redirect from PayMongo after payment
        if (action == "confirm")
        {
            // Get payment intent ID from query string (PayMongo passes it as payment_intent_id)
            var intentId = Request.Query["payment_intent_id"].FirstOrDefault() 
                ?? Request.Query["payment_intent"].FirstOrDefault()
                ?? paymentIntentId 
                ?? TempData["PaymentIntentId"]?.ToString();
            
            if (!string.IsNullOrEmpty(intentId))
            {
                // Confirm payment
                var result = await _paymentService.ConfirmGatewayPaymentAsync(intentId);
                
                if (result.success)
                {
                    TempData["SuccessMessage"] = "Payment confirmed successfully!";
                    return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
                }
                else
                {
                    TempData["ErrorMessage"] = result.message;
                    return RedirectToPage("/Bookings/Payment", new { bookingId = bookingId });
                }
            }
            else
            {
                // If no payment intent ID, try to find the latest pending payment for this booking
                var latestPayment = await _context.Payments
                    .Where(p => p.BookingId == bookingId && p.PaymentStatus == "Pending")
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();
                
                if (latestPayment != null && !string.IsNullOrEmpty(latestPayment.TransactionReference))
                {
                    var result = await _paymentService.ConfirmGatewayPaymentAsync(latestPayment.TransactionReference);
                    
                    if (result.success)
                    {
                        TempData["SuccessMessage"] = "Payment confirmed successfully!";
                        return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
                    }
                }
                
                TempData["ErrorMessage"] = "Could not find payment information. Please try again.";
                return RedirectToPage("/Bookings/Payment", new { bookingId = bookingId });
            }
        }

        PaymentIntentId = paymentIntentId ?? TempData["PaymentIntentId"]?.ToString();
        ClientKey = TempData["ClientKey"]?.ToString();
        PaymentMethodId = paymentMethodId;
        if (!PaymentMethodId.HasValue && TempData["PaymentMethodId"] != null && int.TryParse(TempData["PaymentMethodId"]?.ToString(), out var methodId))
        {
            PaymentMethodId = methodId;
        }

        if (string.IsNullOrEmpty(PaymentIntentId))
        {
            ErrorMessage = "Payment intent not found. Please try again.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostProcessEwalletAsync(int bookingId, string paymentIntentId, int paymentMethodId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        try
        {
            // Map payment method ID to PayMongo type
            var paymentMethodType = paymentMethodId switch
            {
                2 => "gcash",
                5 => "paymaya",
                3 => "qrph",
                _ => "gcash"
            };

            // Create and attach e-wallet payment method server-side
            var result = await _paymentGatewayService.CreateAndAttachEwalletPaymentMethodAsync(
                paymentIntentId,
                paymentMethodType,
                Booking.Renter.FullName,
                Booking.Renter.Email ?? ""
            );

            if (result.Success)
            {
                // Check if we need to redirect to payment provider
                if (result.Status == "awaiting_next_action")
                {
                    // The redirect URL should be in the response, but we'll check payment intent status
                    var statusResult = await _paymentGatewayService.GetPaymentIntentStatusAsync(paymentIntentId);
                    
                    // For e-wallets, PayMongo typically provides a redirect URL
                    // We'll redirect back to this page with action=confirm to check status
                    TempData["PaymentIntentId"] = paymentIntentId;
                    TempData["PaymentMethodId"] = paymentMethodId;
                    return RedirectToPage("/Bookings/PaymentGateway", new 
                    { 
                        bookingId = bookingId, 
                        paymentIntentId = paymentIntentId,
                        action = "check_status"
                    });
                }
                else if (result.Status == "succeeded")
                {
                    // Payment already succeeded - confirm it
                    var confirmResult = await _paymentService.ConfirmGatewayPaymentAsync(paymentIntentId);
                    if (confirmResult.success)
                    {
                        return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
                    }
                }

                // For other statuses, redirect to check status
                TempData["PaymentIntentId"] = paymentIntentId;
                return RedirectToPage("/Bookings/PaymentGateway", new 
                { 
                    bookingId = bookingId, 
                    paymentIntentId = paymentIntentId,
                    action = "confirm"
                });
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to process e-wallet payment";
                PaymentIntentId = paymentIntentId;
                PaymentMethodId = paymentMethodId;
                return Page();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing e-wallet payment");
            ErrorMessage = "An error occurred while processing your payment. Please try again.";
            PaymentIntentId = paymentIntentId;
            PaymentMethodId = paymentMethodId;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostProcessCardAsync(int bookingId, string paymentIntentId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        try
        {
            // Validate card details
            if (string.IsNullOrWhiteSpace(CardPayment.CardNumber) ||
                CardPayment.ExpMonth <= 0 || CardPayment.ExpMonth > 12 ||
                CardPayment.ExpYear < DateTime.Now.Year ||
                string.IsNullOrWhiteSpace(CardPayment.Cvc) ||
                string.IsNullOrWhiteSpace(CardPayment.CardholderName))
            {
                ErrorMessage = "Please fill in all card details correctly.";
                PaymentIntentId = paymentIntentId;
                PaymentMethodId = 6; // Card
                return Page();
            }

            // For PayMongo, create payment method and attach to payment intent
            var cardDetails = new PaymentGatewayService.CardDetails
            {
                CardNumber = CardPayment.CardNumber.Replace(" ", "").Replace("-", ""),
                ExpMonth = CardPayment.ExpMonth,
                ExpYear = CardPayment.ExpYear,
                Cvc = CardPayment.Cvc,
                CardholderName = CardPayment.CardholderName
            };

            Log.Information("Processing card payment for booking {BookingId}, PaymentIntent {PaymentIntentId}", 
                bookingId, paymentIntentId);

            // Create payment method
            var paymentMethodResult = await _paymentGatewayService.CreatePaymentMethodAsync("card", cardDetails);

            if (!paymentMethodResult.Success || string.IsNullOrEmpty(paymentMethodResult.PaymentMethodId))
            {
                Log.Warning("Failed to create payment method: {Error}", paymentMethodResult.ErrorMessage);
                ErrorMessage = paymentMethodResult.ErrorMessage ?? "Failed to process card details. Please check your card information and try again.";
                PaymentIntentId = paymentIntentId;
                PaymentMethodId = 6; // Card
                return Page();
            }

            Log.Information("Payment method created: {PaymentMethodId}, attaching to payment intent {PaymentIntentId}", 
                paymentMethodResult.PaymentMethodId, paymentIntentId);

            // Attach payment method to payment intent
            var attachResult = await _paymentGatewayService.AttachPaymentMethodAsync(
                paymentIntentId,
                paymentMethodResult.PaymentMethodId
            );

            Log.Information("Payment method attached. Status: {Status}, Success: {Success}", 
                attachResult.Status, attachResult.Success);

            if (attachResult.Success)
            {
                // Check payment status
                if (attachResult.Status == "succeeded")
                {
                    // Payment succeeded immediately - confirm it
                    var confirmResult = await _paymentService.ConfirmGatewayPaymentAsync(paymentIntentId);
                    if (confirmResult.success)
                    {
                        return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
                    }
                    else
                    {
                        ErrorMessage = confirmResult.message ?? "Payment succeeded but confirmation failed. Please contact support.";
                        PaymentIntentId = paymentIntentId;
                        PaymentMethodId = 6;
                        return Page();
                    }
                }
                else if (attachResult.Status == "awaiting_next_action")
                {
                    // 3D Secure or other action required - redirect to check status
                    TempData["PaymentIntentId"] = paymentIntentId;
                    return RedirectToPage("/Bookings/PaymentGateway", new 
                    { 
                        bookingId = bookingId, 
                        paymentIntentId = paymentIntentId,
                        action = "confirm"
                    });
                }
                else
                {
                    // Payment is processing - check status
                    TempData["PaymentIntentId"] = paymentIntentId;
                    return RedirectToPage("/Bookings/PaymentGateway", new 
                    { 
                        bookingId = bookingId, 
                        paymentIntentId = paymentIntentId,
                        action = "confirm"
                    });
                }
            }
            else
            {
                Log.Warning("Failed to attach payment method: {Error}", attachResult.ErrorMessage);
                ErrorMessage = attachResult.ErrorMessage ?? "Payment failed. Please check your card details and try again.";
                PaymentIntentId = paymentIntentId;
                PaymentMethodId = 6; // Card
                return Page();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing card payment for booking {BookingId}", bookingId);
            ErrorMessage = "An error occurred while processing your payment. Please try again.";
            PaymentIntentId = paymentIntentId;
            PaymentMethodId = 6; // Card
            return Page();
        }
    }

    public async Task<IActionResult> OnGetConfirmAsync(int bookingId, string paymentIntentId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Confirm payment after redirect from gateway
        var result = await _paymentService.ConfirmGatewayPaymentAsync(paymentIntentId);

        if (result.success)
        {
            return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
        }
        else
        {
            TempData["ErrorMessage"] = result.message;
            return RedirectToPage("/Bookings/Payment", new { bookingId = bookingId });
        }
    }
}

