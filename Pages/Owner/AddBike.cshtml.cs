using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace BiketaBai.Pages.Owner;

public class AddBikeModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AddBikeModel(BiketaBaiDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<BikeType> BikeTypes { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Brand is required")]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "Model is required")]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bike type is required")]
        [Display(Name = "Bike Type")]
        public int BikeTypeId { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Display(Name = "Hourly Rate")]
        [Range(1, 10000, ErrorMessage = "Hourly rate must be between ₱1 and ₱10,000")]
        public decimal? HourlyRate { get; set; }

        [Required(ErrorMessage = "Daily rate is required")]
        [Display(Name = "Daily Rate")]
        [Range(1, 50000, ErrorMessage = "Daily rate must be between ₱1 and ₱50,000")]
        public decimal? DailyRate { get; set; }

        public List<IFormFile>? Images { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Check if owner is verified
        var userId = AuthHelper.GetCurrentUserId(User);
        if (userId.HasValue)
        {
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null && user.IsOwner)
            {
                if (!user.IsVerifiedOwner || user.VerificationStatus != "Approved")
                {
                    TempData["ErrorMessage"] = "⚠️ Your owner account must be verified by an admin before you can list bikes. Please wait for approval or contact support.";
                    return RedirectToPage("/Account/Profile");
                }
                
                // Check bike limit
                var maxBikes = _configuration.GetValue<int>("AppSettings:MaxBikesPerOwner", 10);
                var currentBikeCount = await _context.Bikes
                    .CountAsync(b => b.OwnerId == userId.Value && !b.IsDeleted);
                
                if (currentBikeCount >= maxBikes)
                {
                    TempData["ErrorMessage"] = $"⚠️ You have reached the maximum limit of {maxBikes} bikes. Please remove or delete a bike before adding a new one.";
                    return RedirectToPage("/Owner/MyBikes");
                }
            }
        }

        BikeTypes = await _context.BikeTypes.ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        BikeTypes = await _context.BikeTypes.ToListAsync();

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Check if owner is verified before allowing bike listing
        var user = await _context.Users.FindAsync(userId.Value);
        if (user != null && user.IsOwner)
        {
            if (!user.IsVerifiedOwner || user.VerificationStatus != "Approved")
            {
                TempData["ErrorMessage"] = "⚠️ Your owner account must be verified by an admin before you can list bikes. Please wait for approval or contact support.";
                return RedirectToPage("/Account/Profile");
            }
            
            // Check bike limit
            var maxBikes = _configuration.GetValue<int>("AppSettings:MaxBikesPerOwner", 10);
            var currentBikeCount = await _context.Bikes
                .CountAsync(b => b.OwnerId == userId.Value && !b.IsDeleted);
            
            if (currentBikeCount >= maxBikes)
            {
                TempData["ErrorMessage"] = $"⚠️ You have reached the maximum limit of {maxBikes} bikes. Please remove or delete a bike before adding a new one.";
                return RedirectToPage("/Owner/MyBikes");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // Create bike with streamlined fields
            var bike = new Bike
            {
                OwnerId = userId.Value,
                BikeTypeId = Input.BikeTypeId,
                Brand = Input.Brand,
                Model = Input.Model,
                Description = Input.Description ?? string.Empty,
                HourlyRate = Input.HourlyRate ?? 0,
                DailyRate = Input.DailyRate ?? 0,
                AvailabilityStatusId = 1, // Available
                ViewCount = 0,
                BookingCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Bikes.Add(bike);
            await _context.SaveChangesAsync();

            // Handle image uploads
            if (Input.Images != null && Input.Images.Any())
            {
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "bikes");
                Directory.CreateDirectory(uploadPath);

                int imageIndex = 0;
                foreach (var image in Input.Images.Take(5)) // Limit to 5 images
                {
                    if (image.Length > 0 && image.Length <= 10 * 1024 * 1024) // Max 10MB per image
                    {
                        // Validate image type
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                        
                        if (!allowedExtensions.Contains(extension))
                        {
                            continue; // Skip invalid files
                        }

                        var fileName = $"{bike.BikeId}_{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadPath, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }

                        var bikeImage = new BikeImage
                        {
                            BikeId = bike.BikeId,
                            ImageUrl = $"/uploads/bikes/{fileName}",
                            IsPrimary = imageIndex == 0,
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.BikeImages.Add(bikeImage);
                        imageIndex++;
                    }
                }

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "🎉 Your bike has been listed successfully!";
            return RedirectToPage("/Owner/MyBikes");
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while listing your bike. Please try again.";
            Console.WriteLine($"Error in AddBike: {ex.Message}");
            return Page();
        }
    }
}
