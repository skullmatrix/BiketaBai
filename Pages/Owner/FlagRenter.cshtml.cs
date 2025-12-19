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
public class FlagRenterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterFlagService _renterFlagService;

    public FlagRenterModel(BiketaBaiDbContext context, RenterFlagService renterFlagService)
    {
        _context = context;
        _renterFlagService = renterFlagService;
    }

    public Booking? Booking { get; set; }
    public bool HasFlagged { get; set; }

    [BindProperty]
    public string FlagReason { get; set; } = string.Empty;

    [BindProperty]
    public string? FlagDescription { get; set; }

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

        // Only allow flagging completed bookings
        if (Booking.BookingStatus != "Completed")
        {
            TempData["ErrorMessage"] = "You can only flag renters for completed bookings.";
            return RedirectToPage("/Dashboard/Owner");
        }

        // Check if already flagged
        HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);

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
            TempData["ErrorMessage"] = "You can only flag renters for completed bookings.";
            return RedirectToPage("/Dashboard/Owner");
        }

        if (string.IsNullOrWhiteSpace(FlagReason))
        {
            ModelState.AddModelError(nameof(FlagReason), "Please select a reason for flagging this renter.");
            HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
            return Page();
        }

        // Check if already flagged
        if (await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value))
        {
            TempData["ErrorMessage"] = "You have already flagged this renter for this booking.";
            return RedirectToPage("/Dashboard/Owner");
        }

        var success = await _renterFlagService.FlagRenterAsync(bookingId, userId.Value, FlagReason, FlagDescription);

        if (success)
        {
            TempData["SuccessMessage"] = $"Renter {Booking.Renter.FullName} has been flagged. Administrators will review this report.";
            return RedirectToPage("/Dashboard/Owner");
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to flag renter. Please try again.";
            HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
            return Page();
        }
    }
}

