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
public class ReportDamageModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BikeDamageService _bikeDamageService;
    private readonly IWebHostEnvironment _environment;

    public ReportDamageModel(BiketaBaiDbContext context, BikeDamageService bikeDamageService, IWebHostEnvironment environment)
    {
        _context = context;
        _bikeDamageService = bikeDamageService;
        _environment = environment;
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
    public IFormFile? DamagePhotoFile { get; set; }

    [BindProperty]
    public string? DamageImageUrl { get; set; }

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
            ExistingDamages = new List<BikeDamage>();
            return Page();
        }

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

        // Handle photo upload
        string? photoUrl = DamageImageUrl;
        
        if (DamagePhotoFile != null && DamagePhotoFile.Length > 0)
        {
            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var fileExtension = Path.GetExtension(DamagePhotoFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError(nameof(DamagePhotoFile), "Please upload a valid image file (JPG, PNG, or WEBP).");
                ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
                return Page();
            }

            // Validate file size (max 5MB)
            if (DamagePhotoFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(DamagePhotoFile), "Photo size must be less than 5MB.");
                ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
                return Page();
            }

            try
            {
                // Save file to wwwroot/uploads/damage-photos
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "damage-photos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"damage_{bookingId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await DamagePhotoFile.CopyToAsync(fileStream);
                }

                photoUrl = $"/uploads/damage-photos/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(DamagePhotoFile), $"Error uploading photo: {ex.Message}");
                ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
                return Page();
            }
        }

        try
        {
            var result = await _bikeDamageService.ReportDamageAsync(
                bookingId,
                userId.Value,
                DamageDescription,
                DamageCost,
                DamageDetails,
                photoUrl
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
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while reporting the damage. Please try again.";
            ExistingDamages = await _bikeDamageService.GetDamagesForBookingAsync(bookingId);
            return Page();
        }
    }
}

