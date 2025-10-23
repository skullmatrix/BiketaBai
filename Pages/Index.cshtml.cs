using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;

namespace BiketaBai.Pages;

public class IndexModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public IndexModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public List<BikeType> BikeTypes { get; set; } = new();
    public List<Bike> FeaturedBikes { get; set; } = new();
    public Dictionary<int, double> BikeRatings { get; set; } = new();
    public int TotalBikes { get; set; }
    public int TotalUsers { get; set; }
    public int TotalBookings { get; set; }
    public decimal CO2Saved { get; set; }

    public async Task OnGetAsync()
    {
        BikeTypes = await _context.BikeTypes.ToListAsync();
        
        FeaturedBikes = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Where(b => b.AvailabilityStatusId == 1) // Available
            .OrderByDescending(b => b.CreatedAt)
            .Take(6)
            .ToListAsync();

        // Calculate ratings for featured bikes
        foreach (var bike in FeaturedBikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();
            
            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

        // Statistics
        TotalBikes = await _context.Bikes.CountAsync();
        TotalUsers = await _context.Users.CountAsync();
        TotalBookings = await _context.Bookings.Where(b => b.BookingStatusId == 3).CountAsync(); // Completed
        
        // Calculate CO2 saved (assuming 0.2 kg CO2 per km saved)
        var totalKmSaved = await _context.Bookings
            .Where(b => b.DistanceSavedKm.HasValue)
            .SumAsync(b => b.DistanceSavedKm ?? 0);
        CO2Saved = totalKmSaved * 0.2m;
    }
}

