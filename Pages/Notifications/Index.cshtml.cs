using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Notifications;

[Authorize]
public class IndexModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly NotificationService _notificationService;

    public IndexModel(BiketaBaiDbContext context, NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public List<BiketaBai.Models.Notification> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Get all notifications for the user
        Notifications = await _notificationService.GetUserNotificationsAsync(userId.Value, pageNumber: 1, pageSize: 100);
        UnreadCount = await _notificationService.GetUnreadCountAsync(userId.Value);

        return Page();
    }

    public async Task<IActionResult> OnPostMarkAsReadAsync(int notificationId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        await _notificationService.MarkAsReadAsync(notificationId, userId.Value);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkAllAsReadAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        await _notificationService.MarkAllAsReadAsync(userId.Value);
        return RedirectToPage();
    }
}

