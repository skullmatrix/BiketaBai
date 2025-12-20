using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class RedTaggedRenterDetailsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterRedTagService _redTagService;
    private readonly BikeDamageService _damageService;

    public RedTaggedRenterDetailsModel(BiketaBaiDbContext context, RenterRedTagService redTagService, BikeDamageService damageService)
    {
        _context = context;
        _redTagService = redTagService;
        _damageService = damageService;
    }

    public RenterRedTag? RedTag { get; set; }
    public List<BikeDamage> UnpaidDamages { get; set; } = new();
    public List<Booking> RelatedBookings { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int redTagId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        RedTag = await _context.RenterRedTags
            .Include(t => t.Renter)
            .Include(t => t.Owner)
            .Include(t => t.Booking)
                .ThenInclude(b => b.Bike)
            .FirstOrDefaultAsync(t => t.RedTagId == redTagId && t.OwnerId == userId.Value);

        if (RedTag == null)
            return NotFound();

        // Get unpaid damages for this renter
        UnpaidDamages = await _damageService.GetDamagesForRenterAsync(RedTag.RenterId);

        // Get related bookings
        RelatedBookings = await _context.Bookings
            .Include(b => b.Bike)
            .Where(b => b.RenterId == RedTag.RenterId && b.Bike.OwnerId == userId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .ToListAsync();

        return Page();
    }
}

