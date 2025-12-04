using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BiketaBai.Services;

public class BookingService
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public BookingService(BiketaBaiDbContext context, NotificationService notificationService, IConfiguration configuration)
    {
        _context = context;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<bool> CheckBikeAvailabilityAsync(int bikeId, DateTime startDate, DateTime endDate, int requestedQuantity = 1)
    {
        // Check if bike exists and is not deleted
        var bike = await _context.Bikes.FindAsync(bikeId);
        if (bike == null || bike.IsDeleted)
            return false;

        // Allow bikes with status "Available" or "Rented" (some bikes may still be available)
        if (bike.AvailabilityStatus != "Available" && bike.AvailabilityStatus != "Rented")
            return false; // Only allow Available or Rented bikes

        // Check for conflicting bookings and count how many bikes are already booked
        // Include lost bikes in the calculation since they should be subtracted from available quantity
        var conflictingBookings = await _context.Bookings
            .Where(b => b.BikeId == bikeId &&
                       (b.BookingStatus == "Pending" || b.BookingStatus == "Active") && // Pending or Active (includes lost bikes)
                       ((startDate >= b.StartDate && startDate < b.EndDate) ||
                        (endDate > b.StartDate && endDate <= b.EndDate) ||
                        (startDate <= b.StartDate && endDate >= b.EndDate)))
            .ToListAsync();

        // Sum up the quantity of bikes already booked during this period (including lost bikes)
        var bookedQuantity = conflictingBookings.Sum(b => b.Quantity);

        // Calculate available quantity (ensure Quantity is at least 1 for backward compatibility)
        var bikeQuantity = bike.Quantity > 0 ? bike.Quantity : 1;
        var availableQuantity = bikeQuantity - bookedQuantity;

        // Check if there are enough bikes available
        return availableQuantity >= requestedQuantity;
    }

    public decimal CalculateRentalCost(decimal hourlyRate, decimal dailyRate, DateTime startDate, DateTime endDate)
    {
        var duration = endDate - startDate;
        var hours = duration.TotalHours;
        var days = duration.TotalDays;

        // If rental is more than 24 hours, use daily rate, otherwise use hourly rate
        if (days >= 1)
        {
            var fullDays = (int)Math.Floor(days);
            var remainingHours = (hours - (fullDays * 24));
            return (fullDays * dailyRate) + (remainingHours > 0 ? (decimal)remainingHours * hourlyRate : 0);
        }
        else
        {
            return (decimal)hours * hourlyRate;
        }
    }

    public async Task<(bool success, int bookingId, string message)> CreateBookingAsync(
        int renterId, 
        int bikeId, 
        DateTime startDate, 
        DateTime endDate, 
        int quantity = 1,
        decimal? distanceSavedKm = null,
        string? pickupLocation = null,
        string? returnLocation = null)
    {
        // Validate availability with quantity
        if (!await CheckBikeAvailabilityAsync(bikeId, startDate, endDate, quantity))
            return (false, 0, $"Not enough bikes available. Only {await GetAvailableQuantityAsync(bikeId, startDate, endDate)} bike(s) available for the selected dates.");

        var bike = await _context.Bikes.FindAsync(bikeId);
        if (bike == null)
            return (false, 0, "Bike not found");

        if (quantity > bike.Quantity)
            return (false, 0, $"Cannot book {quantity} bikes. This listing only has {bike.Quantity} bike(s).");

        // Calculate costs (per bike, then multiply by quantity)
        var duration = endDate - startDate;
        var rentalHours = (decimal)duration.TotalHours;
        var baseRatePerBike = CalculateRentalCost(bike.HourlyRate, bike.DailyRate, startDate, endDate);
        var baseRate = baseRatePerBike * quantity; // Total for all bikes
        var serviceFeePercentage = _configuration.GetValue<decimal>("AppSettings:ServiceFeePercentage");
        var serviceFee = baseRate * (serviceFeePercentage / 100);
        var totalAmount = baseRate + serviceFee;

        // Create booking with quantity
        var booking = new Booking
        {
            RenterId = renterId,
            BikeId = bikeId,
            StartDate = startDate,
            EndDate = endDate,
            RentalHours = rentalHours,
            Quantity = quantity,
            BaseRate = baseRate,
            ServiceFee = serviceFee,
            TotalAmount = totalAmount,
            BookingStatus = "Pending",
            DistanceSavedKm = distanceSavedKm,
            PickupLocation = pickupLocation,
            ReturnLocation = returnLocation,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // NOTE: Notification to owner is sent only when payment is processed (in PaymentService)
        // This prevents duplicate notifications - owner only gets notified when payment type is selected
        // For cash payments, notification is sent in PaymentService.ProcessPaymentAsync
        // For other payments, notification is sent when payment is confirmed

        return (true, booking.BookingId, "Booking created successfully");
    }

    private async Task<int> GetAvailableQuantityAsync(int bikeId, DateTime startDate, DateTime endDate)
    {
        var bike = await _context.Bikes.FindAsync(bikeId);
        if (bike == null || bike.IsDeleted) return 0;

        // Include lost bikes in availability calculation since they should be subtracted from available quantity
        var conflictingBookings = await _context.Bookings
            .Where(b => b.BikeId == bikeId &&
                       (b.BookingStatus == "Pending" || b.BookingStatus == "Active") && // Pending or Active (includes lost bikes)
                       ((startDate >= b.StartDate && startDate < b.EndDate) ||
                        (endDate > b.StartDate && endDate <= b.EndDate) ||
                        (startDate <= b.StartDate && endDate >= b.EndDate)))
            .ToListAsync();

        // Sum up the quantity of bikes already booked during this period (including lost bikes)
        var bookedQuantity = conflictingBookings.Sum(b => b.Quantity);
        
        // Calculate available quantity (ensure Quantity is at least 1 for backward compatibility)
        var bikeQuantity = bike.Quantity > 0 ? bike.Quantity : 1;
        return Math.Max(0, bikeQuantity - bookedQuantity);
    }

    public async Task<(bool success, string message)> CancelBookingAsync(int bookingId, int userId, string? reason = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null)
            return (false, "Booking not found");

        if (booking.RenterId != userId && booking.Bike.OwnerId != userId)
            return (false, "Unauthorized to cancel this booking");

        if (booking.BookingStatus == "Completed" || booking.BookingStatus == "Cancelled")
            return (false, "Booking is already completed or cancelled");

        // Calculate refund based on cancellation policy
        decimal refundPercentage = 0;
        var hoursUntilStart = (booking.StartDate - DateTime.UtcNow).TotalHours;
        var freeHours = _configuration.GetValue<int>("AppSettings:CancellationFreeHours");
        var partialRefund = _configuration.GetValue<decimal>("AppSettings:CancellationPartialRefundPercentage");

        if (hoursUntilStart >= freeHours)
            refundPercentage = 100;
        else if (hoursUntilStart > 0)
            refundPercentage = partialRefund;
        else
            refundPercentage = 0;

        booking.BookingStatus = "Cancelled";
        booking.CancellationReason = reason;
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        // Update bike status back to available
        booking.Bike.AvailabilityStatus = "Available";
        booking.Bike.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify parties
        await _notificationService.CreateNotificationAsync(
            booking.RenterId,
            "Booking Cancelled",
            $"Your booking #{bookingId} has been cancelled. Refund: {refundPercentage}%",
            "Booking"
        );

        await _notificationService.CreateNotificationAsync(
            booking.Bike.OwnerId,
            "Booking Cancelled",
            $"Booking #{bookingId} for your bike {booking.Bike.Brand} {booking.Bike.Model} has been cancelled",
            "Booking"
        );

        return (true, $"Booking cancelled. Refund: {refundPercentage}% of total amount");
    }

    public async Task<bool> CompleteBookingAsync(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Bike)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null || booking.BookingStatus != "Active") // Must be Active
            return false;

        booking.BookingStatus = "Completed";
        booking.ActualReturnDate = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        // Update bike status back to available
        booking.Bike.AvailabilityStatus = "Available";
        booking.Bike.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify both parties to rate each other
        await _notificationService.CreateNotificationAsync(
            booking.RenterId,
            "Rental Completed",
            $"Your rental has been completed. Please rate your experience!",
            "Booking",
            $"/Bookings/Details/{bookingId}"
        );

        await _notificationService.CreateNotificationAsync(
            booking.Bike.OwnerId,
            "Rental Completed",
            $"Booking #{bookingId} has been completed. Please rate the renter!",
            "Booking",
            $"/Bookings/Details/{bookingId}"
        );

        return true;
    }

    public async Task<List<Booking>> GetUserBookingsAsync(int userId, bool asRenter, int? statusId = null)
    {
        var query = _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Renter)
            .AsQueryable();

        if (asRenter)
            query = query.Where(b => b.RenterId == userId);
        else
            query = query.Where(b => b.Bike.OwnerId == userId);

        if (statusId.HasValue)
        {
            var statusMap = new Dictionary<int, string>
            {
                { 1, "Pending" },
                { 2, "Active" },
                { 3, "Completed" },
                { 4, "Cancelled" }
            };
            if (statusMap.ContainsKey(statusId.Value))
                query = query.Where(b => b.BookingStatus == statusMap[statusId.Value]);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<Booking?> GetBookingDetailsAsync(int bookingId)
    {
        return await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeImages)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .Include(b => b.Ratings)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
    }
}

