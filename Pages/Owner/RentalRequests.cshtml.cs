using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using System.Security.Claims;

namespace BiketaBai.Pages.Owner;

public class RentalRequestsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<RentalRequestsModel> _logger;

    public RentalRequestsModel(
        BiketaBaiDbContext context,
        NotificationService notificationService,
        ILogger<RentalRequestsModel> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public List<Booking> PendingRequests { get; set; } = new();
    public List<Booking> AcceptedRequests { get; set; } = new();
    public List<Booking> AllRequests { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get user to check if they're an owner
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsOwner)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        // Get all bookings for bikes owned by this user
        var ownerBikes = await _context.Bikes
            .Where(b => b.OwnerId == userId)
            .Select(b => b.BikeId)
            .ToListAsync();

        // Get pending requests (Booking Status ID 1)
        PendingRequests = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(b => b.BikeImages)
            .Include(b => b.Bike)
                .ThenInclude(b => b.BikeType)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .Where(b => ownerBikes.Contains(b.BikeId) && b.BookingStatus == "Pending")
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Get accepted requests (Active - Booking Status ID 2)
        AcceptedRequests = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Where(b => ownerBikes.Contains(b.BikeId) && b.BookingStatus == "Active")
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Get all requests (excluding completed)
        AllRequests = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Where(b => ownerBikes.Contains(b.BikeId) && b.BookingStatus != "Completed")
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get the booking
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking not found.";
            return RedirectToPage();
        }

        // Verify this booking is for a bike owned by the current user
        if (booking.Bike.OwnerId != userId)
        {
            TempData["ErrorMessage"] = "You do not have permission to accept this booking.";
            return RedirectToPage();
        }

        // Check if already accepted
        if (booking.BookingStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This booking has already been processed.";
            return RedirectToPage();
        }

        try
        {
            // Update booking status to Active
            booking.BookingStatus = "Active";
            booking.UpdatedAt = DateTime.UtcNow;

            // Update bike availability to Rented
            booking.Bike.AvailabilityStatus = "Rented";
            booking.Bike.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Mark related notifications as read since owner took action
            await _notificationService.MarkNotificationsByBookingIdAsync(userId, booking.BookingId);

            // Send notification to renter with geofencing link
            var endDatePHT = TimeZoneHelper.FormatPhilippineTime(booking.EndDate);
            await _notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Booking Accepted - Rental Active",
                $"Your rental request for {booking.Bike.Brand} {booking.Bike.Model} has been accepted! Your rental is now active and will end on {endDatePHT}. Please start location tracking for geofencing.",
                "Booking",
                $"/Bookings/TrackLocation/{booking.BookingId}"
            );

            // Send real-time booking update via SignalR to redirect renter to geofencing
            await _notificationService.SendBookingUpdateAsync(
                booking.RenterId,
                booking.BookingId,
                "approved",
                $"Your rental request has been accepted! Your rental is now active and will end on {endDatePHT}.",
                $"/Bookings/TrackLocation/{booking.BookingId}"
            );

            _logger.LogInformation($"Booking {booking.BookingId} accepted by owner {userId}");
            TempData["SuccessMessage"] = $"Rental request from {booking.Renter.FullName} has been accepted!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error accepting booking {booking.BookingId}");
            TempData["ErrorMessage"] = "An error occurred while accepting the booking.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get the booking
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking not found.";
            return RedirectToPage();
        }

        // Verify this booking is for a bike owned by the current user
        if (booking.Bike.OwnerId != userId)
        {
            TempData["ErrorMessage"] = "You do not have permission to reject this booking.";
            return RedirectToPage();
        }

        // Check if already processed
        if (booking.BookingStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This booking has already been processed.";
            return RedirectToPage();
        }

        try
        {
            // Update booking status to Cancelled
            booking.BookingStatus = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;

            // Note: Wallet refund functionality removed. For rejected bookings, payment should be handled through payment gateway refund process.

            await _context.SaveChangesAsync();

            // Mark related notifications as read since owner took action
            await _notificationService.MarkNotificationsByBookingIdAsync(userId, booking.BookingId);

            // Send notification to renter
            await _notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Booking Rejected",
                $"Your rental request for {booking.Bike.Brand} {booking.Bike.Model} has been declined. Please contact support for refund if payment was already made.",
                "/Dashboard/Renter"
            );

            _logger.LogInformation($"Booking {booking.BookingId} rejected by owner {userId}");
            TempData["SuccessMessage"] = $"Rental request from {booking.Renter.FullName} has been rejected. Please contact support for refund if payment was already made.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error rejecting booking {booking.BookingId}");
            TempData["ErrorMessage"] = "An error occurred while rejecting the booking.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVerifyPaymentAsync(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get the booking with payment
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking not found.";
            return RedirectToPage();
        }

        // Verify this booking is for a bike owned by the current user
        if (booking.Bike.OwnerId != userId)
        {
            TempData["ErrorMessage"] = "You do not have permission to verify payment for this booking.";
            return RedirectToPage();
        }

        // Check if booking is pending
        if (booking.BookingStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This booking has already been processed.";
            return RedirectToPage();
        }

        // Find cash payment
        var cashPayment = booking.Payments?.FirstOrDefault(p => p.PaymentMethod == "Cash" && p.PaymentStatus == "Pending");
        if (cashPayment == null)
        {
            TempData["ErrorMessage"] = "No pending cash payment found for this booking.";
            return RedirectToPage();
        }

        // Check if location permission is granted before verifying payment
        if (!booking.LocationPermissionGranted)
        {
            TempData["ErrorMessage"] = $"Cannot verify payment. Renter {booking.Renter.FullName} has not granted location permission. Please advise them to enable location access before you can verify the payment and release the bike.";
            
            // Notify renter that they need to enable location
            await _notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Location Permission Required",
                $"To complete your booking #{booking.BookingId}, please enable location access. The owner cannot verify your payment until location permission is granted.",
                "Booking",
                $"/Bookings/Payment/{booking.BookingId}"
            );
            
            return RedirectToPage();
        }

        try
        {
            // Verify payment
            cashPayment.PaymentStatus = "Completed";
            cashPayment.OwnerVerifiedAt = DateTime.UtcNow;
            cashPayment.OwnerVerifiedBy = userId;
            cashPayment.PaymentDate = DateTime.UtcNow;

            // For cash payments, rental time starts NOW when owner verifies payment
            var verificationTime = DateTime.UtcNow;
            booking.StartDate = verificationTime; // Rental starts when payment is verified
            booking.EndDate = verificationTime.AddHours((double)booking.RentalHours); // End date = start + rental hours
            booking.OwnerConfirmedAt = verificationTime; // Record when owner confirmed

            // Activate booking
            booking.BookingStatus = "Active";
            booking.UpdatedAt = DateTime.UtcNow;

            // Update bike status to Rented
            booking.Bike.AvailabilityStatus = "Rented";
            booking.Bike.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Mark related notifications as read since owner verified payment
            await _notificationService.MarkNotificationsByBookingIdAsync(userId, booking.BookingId);

            // Send notification to renter with geofencing link
            var endDatePHT = TimeZoneHelper.FormatPhilippineTime(booking.EndDate);
            await _notificationService.CreateNotificationAsync(
                booking.RenterId,
                "Payment Verified - Rental Started",
                $"Your cash payment of ₱{booking.TotalAmount:F2} for booking #{booking.BookingId} has been verified. Your rental has started and will end on {endDatePHT}. Please start location tracking for geofencing.",
                "Payment",
                $"/Bookings/TrackLocation/{booking.BookingId}"
            );

            // Send real-time booking update via SignalR to redirect renter to geofencing
            await _notificationService.SendBookingUpdateAsync(
                booking.RenterId,
                booking.BookingId,
                "payment_verified",
                $"Your cash payment has been verified! Your rental has started and will end on {endDatePHT}.",
                $"/Bookings/TrackLocation/{booking.BookingId}"
            );

            _logger.LogInformation($"Cash payment verified for booking {booking.BookingId} by owner {userId}. Rental started at {verificationTime:yyyy-MM-dd HH:mm:ss} UTC, ends at {booking.EndDate:yyyy-MM-dd HH:mm:ss} UTC");
            TempData["SuccessMessage"] = $"Cash payment verified! Booking #{booking.BookingId} is now active. Rental time has started.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying payment for booking {booking.BookingId}");
            TempData["ErrorMessage"] = "An error occurred while verifying payment.";
        }

        return RedirectToPage();
    }
}

