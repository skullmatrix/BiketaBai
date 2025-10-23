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
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        WalletBalance = await _walletService.GetBalanceAsync(userId.Value);

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
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        WalletBalance = await _walletService.GetBalanceAsync(userId.Value);

        // Validate wallet balance if paying via wallet
        if (PaymentMethodId == 1 && WalletBalance < Booking.TotalAmount)
        {
            ErrorMessage = "Insufficient wallet balance. Please top up your wallet or choose another payment method.";
            return Page();
        }

        // Process payment
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
}

