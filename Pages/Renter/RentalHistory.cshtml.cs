using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Renter
{
    [Authorize]
    public class RentalHistoryModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public RentalHistoryModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public List<Booking> CurrentBookings { get; set; } = new List<Booking>();
        public List<Booking> PastBookings { get; set; } = new List<Booking>();
        public List<Booking> CancelledBookings { get; set; } = new List<Booking>();

        // Statistics
        public int TotalRentals { get; set; }
        public decimal TotalSpent { get; set; }
        public int ActiveRentals { get; set; }
        public int CompletedRentals { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            var allBookings = await _context.Bookings
                .Include(b => b.Bike)
                    .ThenInclude(bike => bike.BikeImages)
                .Include(b => b.Bike.BikeType)
                .Include(b => b.Bike.Owner)
                .Include(b => b.BookingStatus)
                .Where(b => b.RenterId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // Categorize bookings
            CurrentBookings = allBookings
                .Where(b => b.BookingStatus.StatusName == "Pending" || 
                           b.BookingStatus.StatusName == "Confirmed" ||
                           b.BookingStatus.StatusName == "Active")
                .ToList();

            PastBookings = allBookings
                .Where(b => b.BookingStatus.StatusName == "Completed")
                .ToList();

            CancelledBookings = allBookings
                .Where(b => b.BookingStatus.StatusName == "Cancelled")
                .ToList();

            // Calculate statistics
            TotalRentals = allBookings.Count();
            TotalSpent = allBookings
                .Where(b => b.BookingStatus.StatusName == "Completed" || 
                           b.BookingStatus.StatusName == "Active")
                .Sum(b => b.TotalAmount);
            ActiveRentals = CurrentBookings.Count;
            CompletedRentals = PastBookings.Count;

            return Page();
        }

        public string GetStatusBadgeClass(string statusName)
        {
            return statusName switch
            {
                "Pending" => "bg-warning",
                "Confirmed" => "bg-info",
                "Active" => "bg-primary",
                "Completed" => "bg-success",
                "Cancelled" => "bg-danger",
                _ => "bg-secondary"
            };
        }

        public string GetStatusIcon(string statusName)
        {
            return statusName switch
            {
                "Pending" => "bi-clock-history",
                "Confirmed" => "bi-check-circle",
                "Active" => "bi-bicycle",
                "Completed" => "bi-check-circle-fill",
                "Cancelled" => "bi-x-circle-fill",
                _ => "bi-question-circle"
            };
        }
    }
}

