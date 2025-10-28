using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Owner;

public class EditBikeModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BikeManagementService _bikeService;

    public EditBikeModel(BiketaBaiDbContext context, BikeManagementService bikeService)
    {
        _context = context;
        _bikeService = bikeService;
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
        [StringLength(100, ErrorMessage = "Brand cannot exceed 100 characters")]
        public string Brand { get; set; } = string.Empty;

        [Required(ErrorMessage = "Model is required")]
        [StringLength(100, ErrorMessage = "Model cannot exceed 100 characters")]
        public string Model { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bike type is required")]
        [Display(Name = "Bike Type")]
        public int BikeTypeId { get; set; }

        [Required(ErrorMessage = "Mileage is required")]
        [Range(0, 1000000, ErrorMessage = "Mileage must be between 0 and 1,000,000")]
        public decimal Mileage { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [StringLength(255, ErrorMessage = "Location cannot exceed 255 characters")]
        public string Location { get; set; } = string.Empty;

        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
        public decimal? Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
        public decimal? Longitude { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Hourly rate is required")]
        [Range(0.01, 10000, ErrorMessage = "Hourly rate must be between ₱0.01 and ₱10,000")]
        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; }

        [Required(ErrorMessage = "Daily rate is required")]
        [Range(0.01, 50000, ErrorMessage = "Daily rate must be between ₱0.01 and ₱50,000")]
        [Display(Name = "Daily Rate")]
        public decimal DailyRate { get; set; }

        [Required(ErrorMessage = "Availability status is required")]
        [Display(Name = "Availability Status")]
        public int AvailabilityStatusId { get; set; }

        [Display(Name = "New Images")]
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
        CurrentBike = await _bikeService.GetBikeByIdAsync(id, userId.Value);
        
        if (CurrentBike == null)
        {
            ErrorMessage = "Bike not found or you don't have permission to edit it";
            return RedirectToPage("/Owner/MyBikes");
        }

        // Get bike statistics
        AverageRating = await _bikeService.GetBikeAverageRatingAsync(id);
        TotalBookings = await _context.Bookings
            .CountAsync(b => b.BikeId == id);

        // Populate input model
        Input.Brand = CurrentBike.Brand;
        Input.Model = CurrentBike.Model;
        Input.BikeTypeId = CurrentBike.BikeTypeId;
        Input.Mileage = CurrentBike.Mileage;
        Input.Location = CurrentBike.Location;
        Input.Latitude = CurrentBike.Latitude;
        Input.Longitude = CurrentBike.Longitude;
        Input.Description = CurrentBike.Description;
        Input.HourlyRate = CurrentBike.HourlyRate;
        Input.DailyRate = CurrentBike.DailyRate;
        Input.AvailabilityStatusId = CurrentBike.AvailabilityStatusId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Load bike types for form
        BikeTypes = await _context.BikeTypes.ToListAsync();

        // Load current bike
        CurrentBike = await _bikeService.GetBikeByIdAsync(id, userId.Value);
        if (CurrentBike == null)
        {
            ErrorMessage = "Bike not found";
            return RedirectToPage("/Owner/MyBikes");
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please correct the errors in the form";
            return Page();
        }

        // Update bike using service
        var (success, message) = await _bikeService.UpdateBikeAsync(
            id,
            userId.Value,
            Input.BikeTypeId,
            Input.Brand,
            Input.Model,
            Input.Mileage,
            Input.Location,
            Input.Latitude,
            Input.Longitude,
            Input.Description,
            Input.HourlyRate,
            Input.DailyRate,
            Input.NewImages
        );

        if (success)
        {
            // Update availability if changed
            if (Input.AvailabilityStatusId != CurrentBike.AvailabilityStatusId)
            {
                var (availSuccess, availMessage) = await _bikeService.SetBikeAvailabilityAsync(
                    id,
                    userId.Value,
                    Input.AvailabilityStatusId
                );

                if (!availSuccess)
                {
                    ErrorMessage = availMessage;
                    return Page();
                }
            }

            SuccessMessage = message;
            return RedirectToPage(new { id = id });
        }
        else
        {
            ErrorMessage = message;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteImageAsync(int id, int imageId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var (success, message) = await _bikeService.DeleteBikeImageAsync(imageId, userId.Value);

        if (success)
        {
            SuccessMessage = message;
        }
        else
        {
            ErrorMessage = message;
        }

        return RedirectToPage(new { id = id });
    }
}

