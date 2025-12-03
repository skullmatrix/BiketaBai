using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Hubs;

namespace BiketaBai.Services;

public class NotificationService
{
    private readonly BiketaBaiDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(BiketaBaiDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task CreateNotificationAsync(int userId, string title, string message, string notificationType, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            NotificationType = notificationType,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send real-time notification via SignalR
        var unreadCount = await GetUnreadCountAsync(userId);
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            notificationId = notification.NotificationId,
            title = title,
            message = message,
            type = notificationType,
            actionUrl = actionUrl,
            unreadCount = unreadCount,
            createdAt = notification.CreatedAt
        });
    }

    public async Task SendBookingUpdateAsync(int userId, int bookingId, string updateType, string message, string? redirectUrl = null)
    {
        // Send booking update via SignalR (e.g., when booking is approved)
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveBookingUpdate", new
        {
            bookingId = bookingId,
            updateType = updateType, // "approved", "rejected", "payment_verified", etc.
            message = message,
            redirectUrl = redirectUrl
        });
    }

    public async Task SendCashPaymentRequestAsync(int ownerId, int bookingId, string renterName, string bikeName, decimal amount)
    {
        // Send cash payment request notification via SignalR to owner
        // This will trigger real-time update on MyBikes and RentalRequests pages
        await _hubContext.Clients.Group($"user_{ownerId}").SendAsync("ReceiveCashPaymentRequest", new
        {
            bookingId = bookingId,
            renterName = renterName,
            bikeName = bikeName,
            amount = amount,
            bookingNumber = bookingId.ToString("D6"),
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int pageNumber = 1, int pageSize = 20)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync();
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }
}

