using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Bookings;

[Authorize]
public class UploadConditionPhotoModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public UploadConditionPhotoModel(BiketaBaiDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public Booking? Booking { get; set; }
    public List<BikeConditionPhoto> ExistingPhotos { get; set; } = new();

    [BindProperty]
    public IFormFile? PhotoFile { get; set; }

    [BindProperty]
    public string? PhotoDescription { get; set; }

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .Include(b => b.BikeConditionPhotos)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Only allow photo upload for Active bookings or Pending bookings with completed payment
        var hasCompletedPayment = Booking.Payments.Any(p => p.PaymentStatus == "Completed");
        if (Booking.BookingStatus != "Active" && !(Booking.BookingStatus == "Pending" && hasCompletedPayment))
        {
            TempData["ErrorMessage"] = "You can only upload condition photos for active or paid bookings.";
            return RedirectToPage("/Bookings/Confirmation", new { bookingId });
        }

        ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Payments)
            .Include(b => b.BikeConditionPhotos)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        // Only allow photo upload for Active bookings or Pending bookings with completed payment
        var hasCompletedPayment = Booking.Payments.Any(p => p.PaymentStatus == "Completed");
        if (Booking.BookingStatus != "Active" && !(Booking.BookingStatus == "Pending" && hasCompletedPayment))
        {
            TempData["ErrorMessage"] = "You can only upload condition photos for active or paid bookings.";
            ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();
            return Page();
        }

        if (PhotoFile == null || PhotoFile.Length == 0)
        {
            ModelState.AddModelError(nameof(PhotoFile), "Please select a photo to upload.");
            ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();
            return Page();
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var fileExtension = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            ModelState.AddModelError(nameof(PhotoFile), "Please upload a valid image file (JPG, PNG, or WEBP).");
            ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();
            return Page();
        }

        // Validate file size (max 5MB)
        if (PhotoFile.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(PhotoFile), "Photo size must be less than 5MB.");
            ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();
            return Page();
        }

        try
        {
            // Save file to wwwroot/uploads/condition-photos
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "condition-photos");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{bookingId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await PhotoFile.CopyToAsync(fileStream);
            }

            var photoUrl = $"/uploads/condition-photos/{uniqueFileName}";

            // Create condition photo record
            var conditionPhoto = new BikeConditionPhoto
            {
                BookingId = bookingId,
                PhotoUrl = photoUrl,
                PhotoDescription = PhotoDescription,
                TakenAt = DateTime.UtcNow,
                TakenByRenter = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.BikeConditionPhotos.Add(conditionPhoto);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bike condition photo uploaded successfully!";
            // Redirect to confirmation page with countdown and geofencing
            return RedirectToPage("/Bookings/Confirmation", new { bookingId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error uploading photo: {ex.Message}");
            ExistingPhotos = Booking.BikeConditionPhotos.OrderByDescending(p => p.TakenAt).ToList();
            return Page();
        }
    }
}

