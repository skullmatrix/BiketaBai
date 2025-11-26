using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class GeofenceSettingsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly GeofencingService _geofencingService;
    private readonly AddressValidationService _addressValidationService;

    public GeofenceSettingsModel(
        BiketaBaiDbContext context,
        GeofencingService geofencingService,
        AddressValidationService addressValidationService)
    {
        _context = context;
        _geofencingService = geofencingService;
        _addressValidationService = addressValidationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Models.User? Owner { get; set; }
    public double? StoreLatitude { get; set; }
    public double? StoreLongitude { get; set; }
    public decimal CurrentGeofenceRadius { get; set; }
    public bool HasStoreAddress { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Range(0.1, 100, ErrorMessage = "Geofence radius must be between 0.1 and 100 kilometers")]
        public decimal? GeofenceRadiusKm { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Owner = await _context.Users.FindAsync(userId.Value);
        if (Owner == null)
            return NotFound();

        HasStoreAddress = !string.IsNullOrWhiteSpace(Owner.StoreAddress);
        
        if (HasStoreAddress)
        {
            // Get or geocode store location
            var (lat, lon) = await _geofencingService.GetStoreLocationAsync(userId.Value);
            StoreLatitude = lat;
            StoreLongitude = lon;
        }

        CurrentGeofenceRadius = Owner.GeofenceRadiusKm ?? _geofencingService.GetDefaultGeofenceRadius();
        Input.GeofenceRadiusKm = Owner.GeofenceRadiusKm;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        if (!ModelState.IsValid)
        {
            return await OnGetAsync();
        }

        Owner = await _context.Users.FindAsync(userId.Value);
        if (Owner == null)
            return NotFound();

        // Update geofence radius
        if (Input.GeofenceRadiusKm.HasValue)
        {
            Owner.GeofenceRadiusKm = Input.GeofenceRadiusKm.Value;
        }
        else
        {
            // Reset to default
            Owner.GeofenceRadiusKm = null;
        }

        // If store address exists but coordinates don't, geocode it
        if (!string.IsNullOrWhiteSpace(Owner.StoreAddress) && 
            (!Owner.StoreLatitude.HasValue || !Owner.StoreLongitude.HasValue))
        {
            var geocodeResult = await _addressValidationService.ValidateAddressAsync(Owner.StoreAddress);
            if (geocodeResult.IsValid && geocodeResult.Latitude.HasValue && geocodeResult.Longitude.HasValue)
            {
                Owner.StoreLatitude = geocodeResult.Latitude.Value;
                Owner.StoreLongitude = geocodeResult.Longitude.Value;
            }
        }

        Owner.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SuccessMessage = "Geofence settings updated successfully!";
        return RedirectToPage();
    }
}

