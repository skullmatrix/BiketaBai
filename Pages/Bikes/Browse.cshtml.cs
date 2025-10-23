using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;

namespace BiketaBai.Pages.Bikes;

public class BrowseModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public BrowseModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public List<Bike> Bikes { get; set; } = new();
    public List<BikeType> BikeTypes { get; set; } = new();
    public Dictionary<int, double> BikeRatings { get; set; } = new();

    public string? FilterLocation { get; set; }
    public int? FilterBikeTypeId { get; set; }
    public decimal? FilterMinPrice { get; set; }
    public decimal? FilterMaxPrice { get; set; }
    public string SortBy { get; set; } = "newest";

    public async Task OnGetAsync(string? location, int? bikeTypeId, decimal? minPrice, decimal? maxPrice, string? sortBy)
    {
        FilterLocation = location;
        FilterBikeTypeId = bikeTypeId;
        FilterMinPrice = minPrice;
        FilterMaxPrice = maxPrice;
        SortBy = sortBy ?? "newest";

        BikeTypes = await _context.BikeTypes.ToListAsync();

        var query = _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => b.AvailabilityStatusId == 1) // Available only
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(b => b.Location.Contains(location));
        }

        if (bikeTypeId.HasValue)
        {
            query = query.Where(b => b.BikeTypeId == bikeTypeId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(b => b.HourlyRate >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(b => b.HourlyRate <= maxPrice.Value);
        }

        // Apply sorting
        query = SortBy switch
        {
            "price-low" => query.OrderBy(b => b.HourlyRate),
            "price-high" => query.OrderByDescending(b => b.HourlyRate),
            "newest" => query.OrderByDescending(b => b.CreatedAt),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        Bikes = await query.ToListAsync();

        // Calculate ratings for bikes
        foreach (var bike in Bikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();

            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

        // Sort by rating if requested
        if (SortBy == "rating")
        {
            Bikes = Bikes.OrderByDescending(b => BikeRatings.ContainsKey(b.BikeId) ? BikeRatings[b.BikeId] : 0).ToList();
        }
    }
}

