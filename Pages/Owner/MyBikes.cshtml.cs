using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

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
    public List<Booking> LostBookings { get; set; } = new();
    public List<Booking> CompletedBookings { get; set; } = new();
    public Dictionary<int, int> AvailableQuantities { get; set; } = new(); // BikeId -> Available Quantity
    public Dictionary<int, List<Booking>> BikeActiveBookings { get; set; } = new(); // BikeId -> List of Active Bookings
    public Dictionary<int, bool> OverdueBookings { get; set; } = new(); // BookingId -> Is Overdue
    public Dictionary<int, bool> RenterRedTaggedStatus { get; set; } = new(); // RenterId -> Is Red Tagged
    public List<(RenterRedTag RedTag, Booking? Booking)> RedTaggedRentersList { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string filter = "all")
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        // Get all bikes for this owner
        var allBikes = await _context.Bikes
            .Include(b => b.BikeType)
            .Include(b => b.BikeImages)
            .Where(b => b.OwnerId == userId.Value && !b.IsDeleted)
            .ToListAsync();

        // Get active bookings with renter info (excluding reported lost)
        ActiveBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
            .Where(b => b.Bike.OwnerId == userId.Value && b.BookingStatus == "Active" && !b.IsReportedLost) // Active and not reported lost
            .OrderBy(b => b.EndDate)
            .ToListAsync();

        // Get lost/not returned bookings
        LostBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
            .Where(b => b.Bike.OwnerId == userId.Value && b.IsReportedLost)
            .OrderByDescending(b => b.ReportedLostAt)
            .ToListAsync();

        // Get completed bookings (for red tagging)
        CompletedBookings = await _context.Bookings
            .Include(b => b.Renter)
            .Include(b => b.Bike)
            .Where(b => b.Bike.OwnerId == userId.Value && b.BookingStatus == "Completed")
            .OrderByDescending(b => b.UpdatedAt)
            .Take(20) // Recent completed bookings
            .ToListAsync();

        // Check which renters are red-tagged and build red tagged renters list
        var redTagService = HttpContext.RequestServices.GetRequiredService<RenterRedTagService>();
        foreach (var booking in CompletedBookings)
        {
            if (!RenterRedTaggedStatus.ContainsKey(booking.RenterId))
            {
                RenterRedTaggedStatus[booking.RenterId] = await redTagService.IsRenterRedTaggedAsync(booking.RenterId);
            }

            // If red-tagged, get the red tag details
            if (RenterRedTaggedStatus[booking.RenterId])
            {
                var activeRedTags = await redTagService.GetActiveRedTagsForRenterAsync(booking.RenterId);
                foreach (var redTag in activeRedTags.Where(rt => rt.OwnerId == userId.Value))
                {
                    if (!RedTaggedRentersList.Any(r => r.RedTag.RedTagId == redTag.RedTagId))
                    {
                        RedTaggedRentersList.Add((redTag, booking));
                    }
                }
            }
        }

        // Group active bookings by bike
        foreach (var bike in allBikes)
        {
            var bikeActiveBookings = ActiveBookings.Where(b => b.BikeId == bike.BikeId).ToList();
            BikeActiveBookings[bike.BikeId] = bikeActiveBookings;

            // Calculate available quantity: Total Quantity - Sum of active bookings quantity - Sum of lost bikes quantity
            // Lost bikes should be subtracted from available quantity since they're not available for rent
            var bikeLostBookings = LostBookings.Where(b => b.BikeId == bike.BikeId).ToList();
            var activeRentedQuantity = bikeActiveBookings.Sum(b => b.Quantity);
            var lostQuantity = bikeLostBookings.Sum(b => b.Quantity);
            var totalRentedQuantity = activeRentedQuantity + lostQuantity;
            
            AvailableQuantities[bike.BikeId] = Math.Max(0, bike.Quantity - totalRentedQuantity);

            // Check for overdue bookings
            var now = DateTime.UtcNow;
            foreach (var booking in bikeActiveBookings)
            {
                OverdueBookings[booking.BookingId] = booking.EndDate < now;
            }
        }

        // Filter bikes based on filter parameter
        if (filter == "active")
        {
            // Only show bikes with active bookings
            var bikesWithActiveBookings = allBikes.Where(b => BikeActiveBookings.ContainsKey(b.BikeId) && BikeActiveBookings[b.BikeId].Any()).ToList();
            Bikes = bikesWithActiveBookings.OrderByDescending(b => b.CreatedAt).ToList();
        }
        else
        {
            Bikes = allBikes.OrderByDescending(b => b.CreatedAt).ToList();
        }

        // Calculate ratings
        foreach (var bike in Bikes)
        {
            var ratings = await _context.Ratings
                .Where(r => r.BikeId == bike.BikeId)
                .Select(r => r.RatingValue)
                .ToListAsync();
            
            BikeRatings[bike.BikeId] = ratings.Any() ? ratings.Average() : 0;
        }

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
                .Where(b => b.BookingStatus == "Pending" || b.BookingStatus == "Active")
                .ToList();

            if (activeBookings.Any())
            {
                TempData["ErrorMessage"] = $"Cannot delete: This bike has {activeBookings.Count} active rental(s). Please wait until all rentals are completed.";
                return RedirectToPage();
            }

            // Check for upcoming bookings
            var upcomingBookings = bike.Bookings
                .Where(b => b.StartDate > DateTime.Now && b.BookingStatus != "Completed") // Not completed
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
            bike.AvailabilityStatus = "Inactive";
            bike.UpdatedAt = DateTime.UtcNow;
            
            // Soft delete completed/cancelled bookings related to this bike
            var oldBookings = bike.Bookings
                .Where(b => b.BookingStatus == "Completed" || b.BookingStatus == "Cancelled")
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

    public async Task<IActionResult> OnPostConfirmReturnAsync(int bookingId)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "You must be logged in";
                return RedirectToPage("/Account/Login");
            }

            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found or you don't have permission";
                return RedirectToPage();
            }

            if (booking.BookingStatus != "Active")
            {
                TempData["ErrorMessage"] = "This booking is not active";
                return RedirectToPage();
            }

            // Mark booking as completed
            booking.BookingStatus = "Completed";
            booking.ActualReturnDate = DateTime.UtcNow;
            booking.OwnerConfirmedAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            // The bike quantity is automatically available again (no need to change AvailabilityStatusId)
            // because availability is calculated dynamically based on active bookings

            await _context.SaveChangesAsync();

            // Send notification to renter
            var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
            await notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Bike Return Confirmed",
                $"Your bike return for booking #{bookingId.ToString("D6")} has been confirmed by the owner.",
                "Booking",
                $"/Bookings/Details/{bookingId}"
            );

            TempData["SuccessMessage"] = $"Bike return confirmed for booking #{bookingId.ToString("D6")}";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error confirming return: {ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostReportLostAsync(int bookingId)
    {
        try
        {
            var userId = AuthHelper.GetCurrentUserId(User);
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "You must be logged in";
                return RedirectToPage("/Account/Login");
            }

            var booking = await _context.Bookings
                .Include(b => b.Bike)
                .Include(b => b.Renter)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Bike.OwnerId == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found or you don't have permission";
                return RedirectToPage();
            }

            if (booking.BookingStatus != "Active")
            {
                TempData["ErrorMessage"] = "This booking is not active";
                return RedirectToPage();
            }

            // Mark booking as reported lost
            booking.IsReportedLost = true;
            booking.ReportedLostAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send notification to renter
            var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
            await notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Bike Reported as Lost",
                $"The owner has reported the bike from booking #{bookingId.ToString("D6")} as lost/not returned. Please contact the owner immediately.",
                "Booking",
                $"/Bookings/Details/{bookingId}"
            );

            TempData["SuccessMessage"] = $"Bike reported as lost for booking #{bookingId.ToString("D6")}. You can view details in Lost Bikes section.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error reporting lost bike: {ex.Message}";
            return RedirectToPage();
        }
    }

}

