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
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var bike = await _context.Bikes
            .Include(b => b.BikeImages)
            .FirstOrDefaultAsync(b => b.BikeId == id && b.OwnerId == userId.Value);

        if (bike == null)
            return NotFound();

        // Check if bike has active bookings
        var hasActiveBookings = await _context.Bookings
            .AnyAsync(b => b.BikeId == id && (b.BookingStatusId == 1 || b.BookingStatusId == 2)); // Pending or Active

        if (hasActiveBookings)
        {
            TempData["ErrorMessage"] = "Cannot delete bike with active bookings";
            return RedirectToPage();
        }

        _context.Bikes.Remove(bike);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Bike deleted successfully";
        return RedirectToPage();
    }
}

