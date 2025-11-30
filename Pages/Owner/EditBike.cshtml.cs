using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Owner;

public class EditBikeModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public EditBikeModel(BiketaBaiDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Bike? CurrentBike { get; set; }
    public List<BikeType> BikeTypes { get; set; } = new();
    public int TotalBookings { get; set; }
    public double AverageRating { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }
    
    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Brand is required")]
        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "Model is required")]
        [StringLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bike type is required")]
        public int BikeTypeId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Range(0.01, 10000)]
        public decimal? HourlyRate { get; set; }

        [Required(ErrorMessage = "Daily rate is required")]
        [Range(0.01, 50000)]
        public decimal? DailyRate { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
        public int Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Availability status is required")]
        public int AvailabilityStatusId { get; set; }

        public List<IFormFile>? NewImages { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Load bike types
        BikeTypes = await _context.BikeTypes.ToListAsync();

        // Load bike details
        CurrentBike = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.AvailabilityStatus)
            .FirstOrDefaultAsync(b => b.BikeId == id && b.OwnerId == userId.Value);
        
        if (CurrentBike == null)
        {
            ErrorMessage = "Bike not found or you don't have permission to edit it";
            return RedirectToPage("/Owner/MyBikes");
        }

        // Get bike statistics
        var ratings = await _context.Ratings
            .Where(r => r.BikeId == id)
            .Select(r => r.RatingValue)
            .ToListAsync();
        AverageRating = ratings.Any() ? ratings.Average() : 0;
        
        TotalBookings = await _context.Bookings
            .CountAsync(b => b.BikeId == id);

        // Populate input model
        Input.Brand = CurrentBike.Brand;
        Input.Model = CurrentBike.Model;
        Input.BikeTypeId = CurrentBike.BikeTypeId;
        Input.Description = CurrentBike.Description;
        Input.HourlyRate = CurrentBike.HourlyRate;
        Input.DailyRate = CurrentBike.DailyRate;
        Input.Quantity = CurrentBike.Quantity;
        Input.AvailabilityStatusId = CurrentBike.AvailabilityStatusId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            if (!AuthHelper.IsOwner(User))
                return RedirectToPage("/Account/AccessDenied");

            // Load bike types for form
            BikeTypes = await _context.BikeTypes.ToListAsync();

            // Load current bike
            CurrentBike = await _context.Bikes
                .Include(b => b.BikeImages)
                .FirstOrDefaultAsync(b => b.BikeId == id && b.OwnerId == userId.Value);
                
            if (CurrentBike == null)
            {
                ErrorMessage = "Bike not found";
                return RedirectToPage("/Owner/MyBikes");
            }

            // Note: We're not validating ModelState because we removed some fields from the form
            // but they still exist in the model (Location, Mileage, etc.)
            // Only validate the fields we're actually updating
            if (string.IsNullOrWhiteSpace(Input.Brand) || string.IsNullOrWhiteSpace(Input.Model) ||
                Input.BikeTypeId <= 0 || !Input.HourlyRate.HasValue || Input.HourlyRate <= 0 || 
                !Input.DailyRate.HasValue || Input.DailyRate <= 0 || Input.Quantity < 1 || Input.Quantity > 100)
            {
                ErrorMessage = "Please fill in all required fields correctly";
                return Page();
            }

            // Update bike details (keep Location, Mileage, Lat/Long unchanged)
            CurrentBike.Brand = Input.Brand;
            CurrentBike.Model = Input.Model;
            CurrentBike.BikeTypeId = Input.BikeTypeId;
            CurrentBike.Description = Input.Description;
            CurrentBike.HourlyRate = Input.HourlyRate.Value;
            CurrentBike.DailyRate = Input.DailyRate.Value;
            CurrentBike.Quantity = Input.Quantity;
            CurrentBike.AvailabilityStatusId = Input.AvailabilityStatusId;
            CurrentBike.UpdatedAt = DateTime.Now;
            // Location, Mileage, Latitude, Longitude remain unchanged

            // Handle new image uploads
            if (Input.NewImages != null && Input.NewImages.Any())
            {
                var totalImages = CurrentBike.BikeImages.Count + Input.NewImages.Count;
                if (totalImages > 5)
                {
                    ErrorMessage = "Maximum 5 photos allowed. Please delete existing photos first.";
                    return Page();
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "bikes");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var image in Input.NewImages)
                {
                    if (image.Length > 5 * 1024 * 1024) // 5MB limit
                    {
                        ErrorMessage = "Image size cannot exceed 5MB";
                        return Page();
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(image.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                    {
                        ErrorMessage = "Only JPG, PNG, and GIF images are allowed";
                        return Page();
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    var bikeImage = new BikeImage
                    {
                        BikeId = CurrentBike.BikeId,
                        ImageUrl = $"/uploads/bikes/{uniqueFileName}",
                        IsPrimary = CurrentBike.BikeImages.Count == 0, // First image is primary
                        UploadedAt = DateTime.Now
                    };

                    _context.BikeImages.Add(bikeImage);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✓ Successfully updated {CurrentBike.Brand} {CurrentBike.Model}";
            return RedirectToPage("/Owner/MyBikes");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating bike: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteImageAsync(int id, int imageId)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            // Load the image with bike ownership check
            var image = await _context.BikeImages
                .Include(bi => bi.Bike)
                .FirstOrDefaultAsync(bi => bi.ImageId == imageId && bi.Bike.OwnerId == userId.Value);

            if (image == null)
            {
                ErrorMessage = "Image not found or you don't have permission to delete it";
                return RedirectToPage(new { id = id });
            }

            // Check if this is the only image
            var imageCount = await _context.BikeImages
                .CountAsync(bi => bi.BikeId == image.BikeId);

            if (imageCount == 1)
            {
                ErrorMessage = "Cannot delete the last photo. A bike must have at least one photo.";
                return RedirectToPage(new { id = id });
            }

            // Delete the file from filesystem
            try
            {
                var filePath = Path.Combine(_environment.WebRootPath, image.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete file {image.ImageUrl}: {ex.Message}");
            }

            // If this was the primary image, set another as primary
            if (image.IsPrimary)
            {
                var newPrimary = await _context.BikeImages
                    .Where(bi => bi.BikeId == image.BikeId && bi.ImageId != imageId)
                    .FirstOrDefaultAsync();

                if (newPrimary != null)
                {
                    newPrimary.IsPrimary = true;
                }
            }

            // Remove the image from database
            _context.BikeImages.Remove(image);
            await _context.SaveChangesAsync();

            SuccessMessage = "✓ Photo deleted successfully";
            return RedirectToPage(new { id = id });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting photo: {ex.Message}";
            return RedirectToPage(new { id = id });
        }
    }
}

