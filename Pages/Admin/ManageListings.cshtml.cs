using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageListingsModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ManageListingsModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public List<Bike> Bikes { get; set; } = new List<Bike>();
        public List<Bike> FilteredBikes { get; set; } = new List<Bike>();
        public List<BikeType> BikeTypes { get; set; } = new List<BikeType>();
        
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }

        // Statistics
        public int TotalListings { get; set; }
        public int AvailableListings { get; set; }
        public int RentedListings { get; set; }
        public int FlaggedListings { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Bikes = await _context.Bikes
                .Include(b => b.Owner)
                .Include(b => b.BikeType)
                .Include(b => b.BikeImages)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            BikeTypes = await _context.BikeTypes.ToListAsync();

            // Calculate statistics
            TotalListings = Bikes.Count;
            AvailableListings = Bikes.Count(b => b.AvailabilityStatus == "Available");
            RentedListings = Bikes.Count(b => b.AvailabilityStatus == "Rented");
            FlaggedListings = Bikes.Count(b => b.AvailabilityStatus == "Maintenance");

            // Apply filters
            FilteredBikes = Bikes;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredBikes = FilteredBikes
                    .Where(b => 
                        b.Brand.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Model.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Owner.FullName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                FilteredBikes = FilteredBikes.Where(b => b.AvailabilityStatus == StatusFilter).ToList();
            }

            // Type filter
            if (!string.IsNullOrWhiteSpace(TypeFilter) && int.TryParse(TypeFilter, out int typeId))
            {
                FilteredBikes = FilteredBikes.Where(b => b.BikeTypeId == typeId).ToList();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int bikeId)
        {
            var bike = await _context.Bikes
                .Include(b => b.Owner)
                .FirstOrDefaultAsync(b => b.BikeId == bikeId);
            
            if (bike == null)
            {
                ErrorMessage = "Bike listing not found";
                return RedirectToPage();
            }

            // Check if bike has active bookings
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => b.BikeId == bikeId && b.BookingStatus == "Active");

            if (hasActiveBookings)
            {
                ErrorMessage = "Cannot delete bike with active bookings. Please wait for rentals to complete.";
                return RedirectToPage();
            }

            _context.Bikes.Remove(bike);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Bike listing '{bike.Brand} {bike.Model}' by {bike.Owner.FullName} has been deleted";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetStatusAsync(int bikeId, string status)
        {
            var bike = await _context.Bikes
                .Include(b => b.Owner)
                .FirstOrDefaultAsync(b => b.BikeId == bikeId);
            
            if (bike == null)
            {
                ErrorMessage = "Bike listing not found";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                ErrorMessage = "Invalid status";
                return RedirectToPage();
            }

            bike.AvailabilityStatus = status;
            bike.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Bike '{bike.Brand} {bike.Model}' status updated to '{status}'";
            return RedirectToPage();
        }

        public string GetStatusBadgeClass(string status)
        {
            return status switch
            {
                "Available" => "bg-success",
                "Rented" => "bg-primary",
                "Inactive" => "bg-secondary",
                "Maintenance" => "bg-warning",
                _ => "bg-secondary"
            };
        }
    }
}

