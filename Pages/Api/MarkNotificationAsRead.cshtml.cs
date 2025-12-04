using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Api;

[Authorize]
[IgnoreAntiforgeryToken]
public class MarkNotificationAsReadModel : PageModel
{
    private readonly NotificationService _notificationService;

    public MarkNotificationAsReadModel(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> OnPostAsync(int notificationId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
        {
            Response.StatusCode = 401;
            return new JsonResult(new { success = false, error = "Unauthorized" });
        }

        try
        {
            await _notificationService.MarkAsReadAsync(notificationId, userId.Value);
            return new JsonResult(new { success = true, notificationId = notificationId });
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }
}

