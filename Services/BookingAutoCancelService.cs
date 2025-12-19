using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BiketaBai.Services;

public class BookingAutoCancelService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookingAutoCancelService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute
    private readonly TimeSpan _autoCancelAfter = TimeSpan.FromMinutes(5); // Auto-cancel after 5 minutes

    public BookingAutoCancelService(
        IServiceProvider serviceProvider,
        ILogger<BookingAutoCancelService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingAutoCancelService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndCancelUnpaidBookingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BookingAutoCancelService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndCancelUnpaidBookingsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BiketaBaiDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var cutoffTime = DateTime.UtcNow.Subtract(_autoCancelAfter);
        
        // Find pending bookings with no completed payment that are older than 5 minutes
        var unpaidBookings = await context.Bookings
            .Include(b => b.Bike)
            .Include(b => b.Renter)
            .Include(b => b.Payments)
            .Where(b => b.BookingStatus == "Pending" &&
                       b.CreatedAt < cutoffTime &&
                       !b.Payments.Any(p => p.PaymentStatus == "Completed"))
            .ToListAsync();

        foreach (var booking in unpaidBookings)
        {
            try
            {
                // Check if it's a cash payment that hasn't been verified
                var hasPendingCashPayment = booking.Payments.Any(p => 
                    p.PaymentMethod == "Cash" && 
                    p.PaymentStatus == "Pending");

                // Only auto-cancel if no payment has been completed
                if (!booking.Payments.Any(p => p.PaymentStatus == "Completed"))
                {
                    booking.BookingStatus = "Cancelled";
                    booking.CancellationReason = "Automatically cancelled due to no payment within 5 minutes";
                    booking.CancelledAt = DateTime.UtcNow;
                    booking.UpdatedAt = DateTime.UtcNow;

                    // Update any pending payments to cancelled
                    foreach (var payment in booking.Payments.Where(p => p.PaymentStatus == "Pending"))
                    {
                        payment.PaymentStatus = "Cancelled";
                        payment.Notes = "Automatically cancelled - no payment received within 5 minutes";
                    }

                    await context.SaveChangesAsync();

                    // Notify renter
                    await notificationService.CreateNotificationAsync(
                        booking.RenterId,
                        "Booking Cancelled",
                        $"Your booking #{booking.BookingId} for {booking.Bike.Brand} {booking.Bike.Model} has been automatically cancelled because no payment was received within 5 minutes.",
                        "Booking",
                        "/Dashboard/Renter"
                    );

                    // Notify owner
                    await notificationService.CreateNotificationAsync(
                        booking.Bike.OwnerId,
                        "Booking Auto-Cancelled",
                        $"Booking #{booking.BookingId} for {booking.Bike.Brand} {booking.Bike.Model} was automatically cancelled due to no payment within 5 minutes.",
                        "Booking",
                        "/Owner/RentalRequests"
                    );

                    _logger.LogInformation($"Auto-cancelled booking {booking.BookingId} - no payment received within 5 minutes");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error auto-cancelling booking {booking.BookingId}");
            }
        }
    }
}


