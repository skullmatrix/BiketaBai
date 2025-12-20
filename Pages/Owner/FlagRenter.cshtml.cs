using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class FlagRenterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterFlagService _renterFlagService;
    private readonly BikeDamageService _bikeDamageService;
    private readonly IWebHostEnvironment _environment;

    public FlagRenterModel(BiketaBaiDbContext context, RenterFlagService renterFlagService, BikeDamageService bikeDamageService, IWebHostEnvironment environment)
    {
        _context = context;
        _renterFlagService = renterFlagService;
        _bikeDamageService = bikeDamageService;
        _environment = environment;
    }

    public Booking? Booking { get; set; }
    public bool HasFlagged { get; set; }

    [BindProperty]
    public string FlagReason { get; set; } = string.Empty;

    [BindProperty]
    public string? FlagDescription { get; set; }

    // Damage-specific fields
    [BindProperty]
    public decimal? DamageCost { get; set; }

    [BindProperty]
    public IFormFile? DamagePhotoFile { get; set; }

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        try
        {
            Booking = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeImages)
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeType)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike != null && b.Bike.OwnerId == userId.Value);
        }
        catch
        {
            TempData["ErrorMessage"] = "An error occurred while loading the booking information.";
            return RedirectToPage("/Dashboard/Owner");
        }

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

        try
        {
            Booking = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike != null && b.Bike.OwnerId == userId.Value);
        }
        catch
        {
            TempData["ErrorMessage"] = "An error occurred while loading the booking information.";
            return RedirectToPage("/Dashboard/Owner");
        }

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

        // Validate damage fields if Damage is selected
        if (FlagReason == "Damage")
        {
            if (!DamageCost.HasValue || DamageCost.Value <= 0)
            {
                ModelState.AddModelError(nameof(DamageCost), "Please enter a valid damage cost greater than zero.");
                HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
                return Page();
            }

            if (string.IsNullOrWhiteSpace(FlagDescription))
            {
                ModelState.AddModelError(nameof(FlagDescription), "Please provide a description of the damage.");
                HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
                return Page();
            }
        }

        // Check if already flagged
        if (await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value))
        {
            TempData["ErrorMessage"] = "You have already flagged this renter for this booking.";
            return RedirectToPage("/Dashboard/Owner");
        }

        try
        {
            // Handle damage photo upload
            string? damagePhotoUrl = null;
            if (FlagReason == "Damage" && DamagePhotoFile != null && DamagePhotoFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var fileExtension = System.IO.Path.GetExtension(DamagePhotoFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError(nameof(DamagePhotoFile), "Please upload a valid image file (JPG, PNG, or WEBP).");
                    HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
                    return Page();
                }

                if (DamagePhotoFile.Length > 5 * 1024 * 1024) // Max 5MB
                {
                    ModelState.AddModelError(nameof(DamagePhotoFile), "Photo size must be less than 5MB.");
                    HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
                    return Page();
                }

                var uploadsFolder = System.IO.Path.Combine(_environment.WebRootPath, "uploads", "damage-photos");
                if (!System.IO.Directory.Exists(uploadsFolder))
                {
                    System.IO.Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"damage_{bookingId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await DamagePhotoFile.CopyToAsync(fileStream);
                }
                damagePhotoUrl = $"/uploads/damage-photos/{uniqueFileName}";
            }

            // Flag the renter (this will also create damage if Damage is selected)
            var success = await _renterFlagService.FlagRenterAsync(
                bookingId, 
                userId.Value, 
                FlagReason, 
                FlagDescription,
                FlagReason == "Damage" ? DamageCost : null,
                damagePhotoUrl
            );

            if (success)
            {
                var renterName = Booking.Renter?.FullName ?? "the renter";
                if (FlagReason == "Damage" && DamageCost.HasValue)
                {
                    TempData["SuccessMessage"] = $"Renter {renterName} has been flagged for damage. Damage charge of ₱{DamageCost.Value:F2} has been applied. The renter has been notified and can pay through their dashboard.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Renter {renterName} has been flagged. Administrators will review this report.";
                }
                return RedirectToPage("/Dashboard/Owner");
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to flag renter. Please try again.";
                HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
                return Page();
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while flagging the renter. Please try again.";
            HasFlagged = await _renterFlagService.HasFlaggedBookingAsync(bookingId, userId.Value);
            return Page();
        }
    }
}

