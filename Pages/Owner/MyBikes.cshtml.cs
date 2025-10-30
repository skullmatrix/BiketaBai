using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Owner;

public class MyBikesModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public MyBikesModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public List<Bike> Bikes { get; set; } = new();
    public Dictionary<int, double> BikeRatings { get; set; } = new();
    public List<Booking> ActiveBookings { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Bikes = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.AvailabilityStatus)
            .Include(b => b.BikeImages)
            .Where(b => b.OwnerId == userId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Calculate ratings
        foreach (var bike in Bikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();
            
            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

        // Get active bookings
        ActiveBookings = await _context.Bookings
            .Where(b => b.Bike.OwnerId == userId.Value && b.BookingStatusId == 2) // Active
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "You must be logged in to delete bikes";
                return RedirectToPage("/Account/Login");
            }

            // Fetch bike with all related data
            var bike = await _context.Bikes
                .Include(b => b.BikeImages)
                .Include(b => b.Bookings)
                .Include(b => b.Ratings)
                .FirstOrDefaultAsync(b => b.BikeId == id && b.OwnerId == userId.Value);

            if (bike == null)
            {
                TempData["ErrorMessage"] = "Bike not found or you don't have permission to delete it";
                return RedirectToPage();
            }

            // Check for active or pending bookings
            var activeBookings = bike.Bookings
                .Where(b => b.BookingStatusId == 1 || b.BookingStatusId == 2)
                .ToList();

            if (activeBookings.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete: This bike has {activeBookings.Count} active rental(s). Please wait until all rentals are completed.";
                return RedirectToPage();
            }

            // Check for upcoming bookings
            var upcomingBookings = bike.Bookings
                .Where(b => b.StartDate > DateTime.Now && b.BookingStatusId != 3) // Not cancelled
                .ToList();

            if (upcomingBookings.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete: This bike has {upcomingBookings.Count} upcoming booking(s). Please cancel them first.";
                return RedirectToPage();
            }

            // Soft delete: Mark as deleted instead of removing from database
            bike.IsDeleted = true;
            bike.DeletedAt = DateTime.UtcNow;
            bike.DeletedBy = AuthHelper.GetUserEmail(User);
            bike.AvailabilityStatusId = 4; // Set to Inactive
            bike.UpdatedAt = DateTime.UtcNow;
            
            // Soft delete completed/cancelled bookings related to this bike
            var oldBookings = bike.Bookings
                .Where(b => b.BookingStatusId == 3 || b.BookingStatusId == 4)
                .ToList();
            
            foreach (var booking in oldBookings)
            {
                booking.IsDeleted = true;
                booking.DeletedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✓ Successfully deleted {bike.Brand} {bike.Model}";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting bike: {ex.Message}";
            return RedirectToPage();
        }
    }
}

