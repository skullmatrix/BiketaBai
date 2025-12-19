using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TestNotificationsModel : PageModel
{
    private readonly NotificationService _notificationService;
    private readonly BiketaBai.Data.BiketaBaiDbContext _context;

    public TestNotificationsModel(
        NotificationService notificationService,
        BiketaBai.Data.BiketaBaiDbContext context)
    {
        _notificationService = notificationService;
        _context = context;
    }

    [BindProperty]
    public string? TestUserId { get; set; }

    [BindProperty]
    public string? NotificationTitle { get; set; } = "Test Notification";

    [BindProperty]
    public string? NotificationMessage { get; set; } = "This is a test notification to verify the notification system is working.";

    [BindProperty]
    public string? NotificationType { get; set; } = "Test";

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public int? CurrentUserId { get; set; }
    public int? UnreadCount { get; set; }

    public void OnGet()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        CurrentUserId = userId;
        
        if (userId.HasValue)
        {
            UnreadCount = _notificationService.GetUnreadCountAsync(userId.Value).Result;
        }
    }

    public async Task<IActionResult> OnPostSendToCurrentUserAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
        {
            ErrorMessage = "You must be logged in to send test notifications.";
            OnGet();
            return Page();
        }

        try
        {
            await _notificationService.CreateNotificationAsync(
                userId.Value,
                NotificationTitle ?? "Test Notification",
                NotificationMessage ?? "This is a test notification.",
                NotificationType ?? "Test",
                "/Admin/TestNotifications"
            );

            SuccessMessage = $"Test notification sent successfully to your account (User ID: {userId.Value})! Check the notification badge and listen for the sound.";
            OnGet();
            return Page();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending test notification");
            ErrorMessage = $"Error: {ex.Message}";
            OnGet();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSendToUserAsync()
    {
        if (string.IsNullOrWhiteSpace(TestUserId) || !int.TryParse(TestUserId, out int userId))
        {
            ErrorMessage = "Please enter a valid user ID.";
            OnGet();
            return Page();
        }

        // Verify user exists
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            ErrorMessage = $"User with ID {userId} not found.";
            OnGet();
            return Page();
        }

        try
        {
            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationTitle ?? "Test Notification",
                NotificationMessage ?? "This is a test notification.",
                NotificationType ?? "Test",
                "/Admin/TestNotifications"
            );

            SuccessMessage = $"Test notification sent successfully to User ID: {userId} ({user.FullName})!";
            OnGet();
            return Page();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending test notification to user {UserId}", userId);
            ErrorMessage = $"Error: {ex.Message}";
            OnGet();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSendBookingUpdateAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
        {
            ErrorMessage = "You must be logged in to send test booking updates.";
            OnGet();
            return Page();
        }

        try
        {
            await _notificationService.SendBookingUpdateAsync(
                userId.Value,
                999999, // Fake booking ID for testing
                "test_update",
                NotificationMessage ?? "This is a test booking update.",
                "/Admin/TestNotifications"
            );

            SuccessMessage = "Test booking update sent successfully! Check the notification badge and listen for the sound.";
            OnGet();
            return Page();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending test booking update");
            ErrorMessage = $"Error: {ex.Message}";
            OnGet();
            return Page();
        }
    }
}









