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

        // If no bikes found, return early
        if (!allBikes.Any())
        {
            Bikes = new List<Bike>();
            return;
        }

        // Calculate available quantity for each bike and filter out bikes with 0 available
        // Get all active and pending bookings for all bikes in one query (optimize N+1)
        // Include ALL pending bookings regardless of start date to prevent double-booking
        var bikeIds = allBikes.Select(b => b.BikeId).ToList();
        
        // Only query bookings if there are bikes to check
        Dictionary<int, int> allActiveBookings = new Dictionary<int, int>();
        if (bikeIds.Any())
        {
            allActiveBookings = await _context.Bookings
                .Where(b => bikeIds.Contains(b.BikeId) && 
                           (b.BookingStatusId == 1 || b.BookingStatusId == 2)) // Pending or Active
                .GroupBy(b => b.BikeId)
                .Select(g => new { BikeId = g.Key, RentedQuantity = g.Sum(b => b.Quantity) })
                .ToDictionaryAsync(x => x.BikeId, x => x.RentedQuantity);
        }
        
        // Calculate available quantity for each bike
        // Show all bikes that are marked as available, but we'll calculate and display available quantity
        // This ensures bikes with some available quantity are still shown
        var bikesWithAvailability = new List<Bike>();
        foreach (var bike in allBikes)
        {
            // Get rented quantity for this bike (default to 0 if no bookings)
            var rentedQuantity = allActiveBookings.GetValueOrDefault(bike.BikeId, 0);
            
            // Calculate available quantity
            // If Quantity is 0 or not set, default to 1 (for backward compatibility with old bikes)
            var bikeQuantity = bike.Quantity > 0 ? bike.Quantity : 1;
            var availableQuantity = bikeQuantity - rentedQuantity;
            
            // Only include bikes with available quantity > 0
            // This ensures bikes that are fully rented don't appear
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

