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

        // Get all bikes that are marked as available (not deleted, not maintenance, etc.)
        var query = _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Include(b => b.Owner)
            .Where(b => b.AvailabilityStatusId == 1 && !b.IsDeleted) // Available and not deleted
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

        var allBikes = await query.ToListAsync();

        // Calculate available quantity for each bike and filter out bikes with 0 available
        var bikesWithAvailability = new List<Bike>();
        foreach (var bike in allBikes)
        {
            // Get active and pending bookings for this bike
            var activeBookings = await _context.Bookings
                .Where(b => b.BikeId == bike.BikeId && 
                           (b.BookingStatusId == 1 || b.BookingStatusId == 2)) // Pending or Active
                .ToListAsync();
            
            // Calculate rented quantity
            var rentedQuantity = activeBookings.Sum(b => b.Quantity);
            
            // Calculate available quantity
            var availableQuantity = bike.Quantity - rentedQuantity;
            
            // Only include bikes with available quantity > 0
            if (availableQuantity > 0)
            {
                bikesWithAvailability.Add(bike);
            }
        }

        Bikes = bikesWithAvailability;

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

