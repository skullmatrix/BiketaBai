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
            // For PayMongo, create payment method and attach to payment intent
            var cardDetails = new PaymentGatewayService.CardDetails
            {
                CardNumber = CardPayment.CardNumber.Replace(" ", ""),
                ExpMonth = CardPayment.ExpMonth,
                ExpYear = CardPayment.ExpYear,
                Cvc = CardPayment.Cvc,
                CardholderName = CardPayment.CardholderName
            };

            // Get booking details
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                ErrorMessage = "Booking not found";
                return Page();
            }

            // Create payment method
            var paymentMethodResult = await _paymentGatewayService.CreatePaymentMethodAsync("card", cardDetails);

            if (!paymentMethodResult.Success || string.IsNullOrEmpty(paymentMethodResult.PaymentMethodId))
            {
                ErrorMessage = paymentMethodResult.ErrorMessage ?? "Failed to process card details";
                PaymentIntentId = paymentIntentId;
                return Page();
            }

            // Attach payment method to payment intent
            var attachResult = await _paymentGatewayService.AttachPaymentMethodAsync(
                paymentIntentId,
                paymentMethodResult.PaymentMethodId
            );

            if (attachResult.Success && attachResult.Status == "succeeded")
            {
                // Create payment record
                var payment = new Payment
                {
                    BookingId = bookingId,
                    PaymentMethod = "Credit/Debit Card",
                    Amount = booking.TotalAmount,
                    PaymentStatus = "Completed",
                    TransactionReference = attachResult.TransactionReference ?? paymentIntentId,
                    PaymentDate = DateTime.UtcNow
                };

                _context.Payments.Add(payment);

                // Update booking status
                booking.BookingStatus = "Active";
                booking.UpdatedAt = DateTime.UtcNow;

                // Update bike status
                booking.Bike.AvailabilityStatus = "Rented";
                booking.Bike.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
            }
            else
            {
                ErrorMessage = attachResult.ErrorMessage ?? "Payment failed. Please check your card details and try again.";
                PaymentIntentId = paymentIntentId;
                return Page();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing card payment");
            ErrorMessage = "An error occurred while processing your payment. Please try again.";
            PaymentIntentId = paymentIntentId;
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

