using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
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
    public Store? PrimaryStore { get; set; }
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

        // Get primary store
        PrimaryStore = await _context.Stores
            .FirstOrDefaultAsync(s => s.OwnerId == userId.Value && s.IsPrimary && !s.IsDeleted);

        HasStoreAddress = PrimaryStore != null && !string.IsNullOrWhiteSpace(PrimaryStore.StoreAddress);
        
        if (HasStoreAddress && PrimaryStore != null)
        {
            // Get or geocode store location
            var (lat, lon) = await _geofencingService.GetStoreLocationAsync(userId.Value);
            StoreLatitude = lat;
            StoreLongitude = lon;
        }

        CurrentGeofenceRadius = PrimaryStore?.GeofenceRadiusKm ?? _geofencingService.GetDefaultGeofenceRadius();
        Input.GeofenceRadiusKm = PrimaryStore?.GeofenceRadiusKm;

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

        // Get or create primary store
        var primaryStore = await _context.Stores
            .FirstOrDefaultAsync(s => s.OwnerId == userId.Value && s.IsPrimary && !s.IsDeleted);

        if (primaryStore == null)
        {
            ErrorMessage = "Store not found. Please set up your store first.";
            return await OnGetAsync();
        }

        // Update geofence radius in Store
        if (Input.GeofenceRadiusKm.HasValue)
        {
            primaryStore.GeofenceRadiusKm = Input.GeofenceRadiusKm.Value;
        }
        else
        {
            // Reset to null (will use default)
            primaryStore.GeofenceRadiusKm = null;
        }

        // If store address exists but coordinates don't, geocode it
        if (!string.IsNullOrWhiteSpace(primaryStore.StoreAddress) && 
            (!primaryStore.StoreLatitude.HasValue || !primaryStore.StoreLongitude.HasValue))
        {
            var geocodeResult = await _addressValidationService.ValidateAddressAsync(primaryStore.StoreAddress);
            if (geocodeResult.IsValid && geocodeResult.Latitude.HasValue && geocodeResult.Longitude.HasValue)
            {
                primaryStore.StoreLatitude = geocodeResult.Latitude.Value;
                primaryStore.StoreLongitude = geocodeResult.Longitude.Value;
                primaryStore.UpdatedAt = DateTime.UtcNow;
            }
        }

        primaryStore.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SuccessMessage = "Geofence settings updated successfully!";
        return RedirectToPage();
    }
}

