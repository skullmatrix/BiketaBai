using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class ReportDamageModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BikeDamageService _bikeDamageService;

    public ReportDamageModel(BiketaBaiDbContext context, BikeDamageService bikeDamageService)
    {
        _context = context;
        _bikeDamageService = bikeDamageService;
    }

    public Booking? Booking { get; set; }
    public List<BikeDamage> ExistingDamages { get; set; } = new();

    [BindProperty]
    public string DamageDescription { get; set; } = string.Empty;

    [BindProperty]
    public string? DamageDetails { get; set; }

    [BindProperty]
    public decimal DamageCost { get; set; }

    [BindProperty]
    public string? DamageImageUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Bike.BikeType)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Only allow damage reporting for completed bookings
        if (Booking.BookingStatus != "Completed")
        {
            TempData["ErrorMessage"] = "You can only report damages for completed bookings.";
            return RedirectToPage("/Dashboard/Owner");
        }

        // Get existing damages for this booking
        ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value);

        if (Booking == null)
            return NotFound();

        if (Booking.BookingStatus != "Completed")
        {
            TempData["ErrorMessage"] = "You can only report damages for completed bookings.";
            ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(DamageDescription))
        {
            ModelState.AddModelError(nameof(DamageDescription), "Please provide a damage description.");
            ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
            return Page();
        }

        if (DamageCost <= 0)
        {
            ModelState.AddModelError(nameof(DamageCost), "Damage cost must be greater than zero.");
            ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
            return Page();
        }

        var result = await _bikeDamageService.ReportDamageAsync(
            bookingId,
            userId.Value,
            DamageDescription,
            DamageCost,
            DamageDetails,
            DamageImageUrl
        );

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Dashboard/Owner");
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
            ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
            return Page();
        }
    }
}

