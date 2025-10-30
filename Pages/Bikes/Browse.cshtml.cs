using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;

namespace BiketaBai.Pages.Bikes;

public class BrowseModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BookingManagementService _bookingService;

    public BrowseModel(BiketaBaiDbContext context, BookingManagementService bookingService)
    {
        _context = context;
        _bookingService = bookingService;
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

        // Calculate ratings for bikes using BookingManagementService
        foreach (var bike in Bikes)
        {
            BikeRatings[bike.BikeId] = await _bookingService.GetBikeAverageRatingAsync(bike.BikeId);
        }

        // Sort by rating if requested
        if (SortBy == "rating")
        {
            Bikes = Bikes.OrderByDescending(b => BikeRatings.ContainsKey(b.BikeId) ? BikeRatings[b.BikeId] : 0).ToList();
        }
    }
}

