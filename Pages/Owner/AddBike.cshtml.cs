using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Owner;

public class AddBikeModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public AddBikeModel(BiketaBaiDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<BikeType> BikeTypes { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        public string Brand { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Bike Type")]
        public int BikeTypeId { get; set; }

        [Required]
        public decimal Mileage { get; set; }

        [Required]
        public string Location { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public string? Description { get; set; }

        [Required]
        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; }

        [Required]
        [Display(Name = "Daily Rate")]
        public decimal DailyRate { get; set; }

        public List<IFormFile>? Images { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

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

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Create bike
        var bike = new Bike
        {
            OwnerId = userId.Value,
            BikeTypeId = Input.BikeTypeId,
            Brand = Input.Brand,
            Model = Input.Model,
            Mileage = Input.Mileage,
            Location = Input.Location,
            Latitude = Input.Latitude,
            Longitude = Input.Longitude,
            Description = Input.Description,
            HourlyRate = Input.HourlyRate,
            DailyRate = Input.DailyRate,
            AvailabilityStatusId = 1, // Available
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
                if (image.Length > 0)
                {
                    var fileName = $"{bike.BikeId}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
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

        TempData["SuccessMessage"] = "Bike listed successfully!";
        return RedirectToPage("/Owner/MyBikes");
    }
}

