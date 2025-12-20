using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Renter;

[Authorize]
public class DamagesModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BikeDamageService _bikeDamageService;
    private readonly PaymentService _paymentService;

    public DamagesModel(BiketaBaiDbContext context, BikeDamageService bikeDamageService, PaymentService paymentService)
    {
        _context = context;
        _bikeDamageService = bikeDamageService;
        _paymentService = paymentService;
    }

    public List<BikeDamage> PendingDamages { get; set; } = new();
    public List<BikeDamage> PaidDamages { get; set; } = new();
    public decimal TotalPendingAmount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsRenter(User))
            return RedirectToPage("/Account/AccessDenied");

        try
        {
            var allDamages = await _context.BikeDamages
                .Include(d => d.Owner)
                .Include(d => d.Bike)
                .Include(d => d.Booking)
                .Where(d => d.RenterId == userId.Value)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            PendingDamages = allDamages.Where(d => d.DamageStatus == "Pending").ToList();
            PaidDamages = allDamages.Where(d => d.DamageStatus == "Paid").ToList();
            TotalPendingAmount = PendingDamages.Sum(d => d.DamageCost);
        }
        catch
        {
            TempData["ErrorMessage"] = "An error occurred while loading damage records.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPayDamageAsync(int damageId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsRenter(User))
            return RedirectToPage("/Account/AccessDenied");

        var damage = await _context.BikeDamages
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d => d.DamageId == damageId && d.RenterId == userId.Value);

        if (damage == null)
        {
            TempData["ErrorMessage"] = "Damage record not found.";
            return RedirectToPage();
        }

        if (damage.DamageStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This damage has already been paid or resolved.";
            return RedirectToPage();
        }

        // Redirect to payment page with damage amount
        return RedirectToPage("/Renter/PayDamage", new { damageId = damageId });
    }
}

