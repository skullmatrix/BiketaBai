using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Admin;

[Authorize]
public class FlagsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterRedTagService _redTagService;
    private readonly BikeDamageService _damageService;

    public FlagsModel(BiketaBaiDbContext context, RenterRedTagService redTagService, BikeDamageService damageService)
    {
        _context = context;
        _redTagService = redTagService;
        _damageService = damageService;
    }

    public List<RenterFlag> Flags { get; set; } = new();
    public List<RenterFlag> PendingFlags { get; set; } = new();
    public List<RenterFlag> ResolvedFlags { get; set; } = new();
    public string Filter { get; set; } = "pending";

    [BindProperty]
    public RedTagInputModel RedTagInput { get; set; } = new();

    public class RedTagInputModel
    {
        [Required]
        public int FlagId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string filter = "pending")
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        Filter = filter;

        var allFlags = await _context.RenterFlags
            .Include(f => f.Owner)
            .Include(f => f.Renter)
            .Include(f => f.Booking)
                .ThenInclude(b => b.Bike)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        PendingFlags = allFlags.Where(f => !f.IsResolved).ToList();
        ResolvedFlags = allFlags.Where(f => f.IsResolved).ToList();

        Flags = filter == "resolved" ? ResolvedFlags : PendingFlags;

        return Page();
    }

    public async Task<IActionResult> OnPostRedTagAsync()
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        if (!ModelState.IsValid)
        {
            Filter = "pending";
            var allFlags = await _context.RenterFlags
                .Include(f => f.Owner)
                .Include(f => f.Renter)
                .Include(f => f.Booking)
                    .ThenInclude(b => b.Bike)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
            PendingFlags = allFlags.Where(f => !f.IsResolved).ToList();
            Flags = PendingFlags;
            return Page();
        }

        // Get the flag
        var flag = await _context.RenterFlags
            .Include(f => f.Booking)
            .Include(f => f.Renter)
            .Include(f => f.Owner)
            .FirstOrDefaultAsync(f => f.FlagId == RedTagInput.FlagId);

        if (flag == null)
        {
            TempData["ErrorMessage"] = "Flag not found.";
            return RedirectToPage();
        }

        // Check if renter is already red-tagged
        var isAlreadyRedTagged = await _redTagService.IsRenterRedTaggedAsync(flag.RenterId);
        if (isAlreadyRedTagged)
        {
            TempData["ErrorMessage"] = "This renter is already red-tagged.";
            return RedirectToPage();
        }

        // Admin can red tag based on flag
        var adminId = AuthHelper.GetCurrentUserId(User);
        if (!adminId.HasValue)
        {
            TempData["ErrorMessage"] = "User not authenticated.";
            return RedirectToPage();
        }
        
        // Create red tag (admin is acting on behalf of the owner who flagged)
        var redTag = new RenterRedTag
        {
            RenterId = flag.RenterId,
            OwnerId = flag.OwnerId, // Keep the original owner who flagged
            BookingId = flag.BookingId,
            RedTagReason = RedTagInput.Reason,
            RedTagDescription = RedTagInput.Description ?? $"Based on flag: {flag.FlagReason}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.RenterRedTags.Add(redTag);

        // Mark flag as resolved
        flag.IsResolved = true;
        flag.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify renter
        var notificationService = HttpContext.RequestServices.GetRequiredService<NotificationService>();
        
        await notificationService.CreateNotificationAsync(
            flag.RenterId,
            "Account Red Tagged",
            $"Your account has been red-tagged by an administrator based on a flag from {flag.Owner?.FullName ?? "an owner"}. Reason: {RedTagInput.Reason}. This may affect your ability to make future bookings.",
            "RedTag",
            "/Dashboard/Renter"
        );

        // Notify owner
        await notificationService.CreateNotificationAsync(
            flag.OwnerId,
            "Renter Red Tagged",
            $"The renter you flagged has been red-tagged by an administrator. Reason: {RedTagInput.Reason}",
            "RedTag",
            $"/Owner/MyBikes"
        );

        TempData["SuccessMessage"] = $"Renter {flag.Renter?.FullName ?? "Unknown"} has been red-tagged successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResolveFlagAsync(int flagId, string? resolutionNotes = null)
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        var flag = await _context.RenterFlags
            .Include(f => f.Renter)
            .Include(f => f.Owner)
            .FirstOrDefaultAsync(f => f.FlagId == flagId);

        if (flag == null)
        {
            TempData["ErrorMessage"] = "Flag not found.";
            return RedirectToPage();
        }

        flag.IsResolved = true;
        flag.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Flag marked as resolved.";
        return RedirectToPage();
    }
}

