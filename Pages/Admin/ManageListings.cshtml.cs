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
                .Include(b => b.AvailabilityStatus)
                .Include(b => b.BikeImages)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            BikeTypes = await _context.BikeTypes.ToListAsync();

            // Calculate statistics
            TotalListings = Bikes.Count;
            AvailableListings = Bikes.Count(b => b.AvailabilityStatusId == 1); // Available
            RentedListings = Bikes.Count(b => b.AvailabilityStatusId == 2); // Rented
            FlaggedListings = Bikes.Count(b => b.AvailabilityStatusId == 4); // Maintenance/Flagged

            // Apply filters
            FilteredBikes = Bikes;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredBikes = FilteredBikes
                    .Where(b => 
                        b.Brand.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Model.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Location.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        b.Owner.FullName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(StatusFilter) && int.TryParse(StatusFilter, out int statusId))
            {
                FilteredBikes = FilteredBikes.Where(b => b.AvailabilityStatusId == statusId).ToList();
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
                .AnyAsync(b => b.BikeId == bikeId && 
                    (b.BookingStatus.StatusName == "Active" || b.BookingStatus.StatusName == "Confirmed"));

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

        public async Task<IActionResult> OnPostSetStatusAsync(int bikeId, int statusId)
        {
            var bike = await _context.Bikes
                .Include(b => b.Owner)
                .FirstOrDefaultAsync(b => b.BikeId == bikeId);
            
            if (bike == null)
            {
                ErrorMessage = "Bike listing not found";
                return RedirectToPage();
            }

            var status = await _context.AvailabilityStatuses.FindAsync(statusId);
            if (status == null)
            {
                ErrorMessage = "Invalid status";
                return RedirectToPage();
            }

            bike.AvailabilityStatusId = statusId;
            bike.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            SuccessMessage = $"Bike '{bike.Brand} {bike.Model}' status updated to '{status.StatusName}'";
            return RedirectToPage();
        }

        public string GetStatusBadgeClass(int statusId)
        {
            return statusId switch
            {
                1 => "bg-success", // Available
                2 => "bg-primary", // Rented
                3 => "bg-secondary", // Inactive
                4 => "bg-warning", // Under Maintenance
                _ => "bg-secondary"
            };
        }
    }
}

