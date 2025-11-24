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

    public async Task<IActionResult> OnGetAsync(int bookingId, string? paymentIntentId, string? action)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
            .ThenInclude(bike => bike.Owner)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Handle redirect from Xendit after payment
        if (action == "confirm")
        {
            // Get invoice ID from query string (Xendit passes it as invoice_id)
            var invoiceId = Request.Query["invoice_id"].FirstOrDefault() 
                ?? paymentIntentId 
                ?? TempData["PaymentIntentId"]?.ToString();
            
            if (!string.IsNullOrEmpty(invoiceId))
            {
                // Confirm payment
                var result = await _paymentService.ConfirmGatewayPaymentAsync(invoiceId);
                
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
                // If no invoice ID, try to find the latest pending payment for this booking
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
            // For Xendit, create token and charge directly
            var cardDetails = new PaymentGatewayService.CardDetails
            {
                CardNumber = CardPayment.CardNumber.Replace(" ", ""),
                ExpMonth = CardPayment.ExpMonth,
                ExpYear = CardPayment.ExpYear,
                Cvc = CardPayment.Cvc,
                CardholderName = CardPayment.CardholderName
            };

            // Create payment method (token)
            var paymentMethodResult = await _paymentGatewayService.CreatePaymentMethodAsync("card", cardDetails);

            if (!paymentMethodResult.Success || string.IsNullOrEmpty(paymentMethodResult.PaymentMethodId))
            {
                ErrorMessage = paymentMethodResult.ErrorMessage ?? "Failed to process card details";
                PaymentIntentId = paymentIntentId;
                return Page();
            }

            // Charge the card using the token
            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                ErrorMessage = "Booking not found";
                return Page();
            }

            var chargeResult = await _paymentGatewayService.ChargeCardAsync(
                paymentMethodResult.PaymentMethodId,
                booking.TotalAmount,
                "PHP",
                $"Booking payment for {booking.Bike.Brand} {booking.Bike.Model}",
                new Dictionary<string, string>
                {
                    { "booking_id", bookingId.ToString() },
                    { "renter_id", booking.RenterId.ToString() },
                    { "cardholder_name", CardPayment.CardholderName },
                    { "cvv", CardPayment.Cvc }
                }
            );

            if (chargeResult.Success)
            {
                // Create payment record
                var payment = new Payment
                {
                    BookingId = bookingId,
                    PaymentMethodId = 6, // Credit/Debit Card
                    Amount = booking.TotalAmount,
                    PaymentStatus = "Completed",
                    TransactionReference = chargeResult.TransactionReference ?? paymentMethodResult.PaymentMethodId,
                    PaymentDate = DateTime.UtcNow
                };

                _context.Payments.Add(payment);

                // Update booking status
                booking.BookingStatusId = 2; // Active
                booking.UpdatedAt = DateTime.UtcNow;

                // Update bike status
                booking.Bike.AvailabilityStatusId = 2; // Rented
                booking.Bike.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return RedirectToPage("/Bookings/Confirmation", new { bookingId = bookingId });
            }
            else
            {
                ErrorMessage = chargeResult.ErrorMessage ?? "Payment failed. Please check your card details and try again.";
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

