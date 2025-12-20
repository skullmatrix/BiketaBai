using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Renter;

[Authorize]
public class PayDamageModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly BikeDamageService _bikeDamageService;
    private readonly PaymentService _paymentService;

    public PayDamageModel(BiketaBaiDbContext context, BikeDamageService bikeDamageService, PaymentService paymentService)
    {
        _context = context;
        _bikeDamageService = bikeDamageService;
        _paymentService = paymentService;
    }

    public BikeDamage? Damage { get; set; }

    [BindProperty]
    public int PaymentMethodId { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int damageId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsRenter(User))
            return RedirectToPage("/Account/AccessDenied");

        // First check if damage exists at all
        var damageExists = await _context.BikeDamages
            .AnyAsync(d => d.DamageId == damageId);

        if (!damageExists)
        {
            TempData["ErrorMessage"] = "Damage record not found. The damage record may not have been created properly.";
            ErrorMessage = "Damage record not found. The damage record may not have been created properly.";
            return RedirectToPage("/Renter/Damages");
        }

        // Then check if it belongs to this renter
        Damage = await _context.BikeDamages
            .Include(d => d.Owner)
            .Include(d => d.Bike)
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d => d.DamageId == damageId && d.RenterId == userId.Value);

        if (Damage == null)
        {
            // Check if damage exists but belongs to different renter
            var damageForOtherRenter = await _context.BikeDamages
                .FirstOrDefaultAsync(d => d.DamageId == damageId);
            
            if (damageForOtherRenter != null && damageForOtherRenter.RenterId != userId.Value)
            {
                TempData["ErrorMessage"] = "You do not have permission to access this damage record.";
                ErrorMessage = "You do not have permission to access this damage record.";
            }
            else
            {
                TempData["ErrorMessage"] = "Damage record not found or you do not have permission to access it.";
                ErrorMessage = "Damage record not found or you do not have permission to access it.";
            }
            return RedirectToPage("/Renter/Damages");
        }

        if (Damage.DamageStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This damage has already been paid or resolved.";
            ErrorMessage = "This damage has already been paid or resolved.";
            return RedirectToPage("/Renter/Damages");
        }

        // Set error/success messages from TempData if present
        if (TempData["ErrorMessage"] != null)
            ErrorMessage = TempData["ErrorMessage"].ToString();
        if (TempData["SuccessMessage"] != null)
            SuccessMessage = TempData["SuccessMessage"].ToString();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int damageId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsRenter(User))
            return RedirectToPage("/Account/AccessDenied");

        // First check if damage exists at all
        var damageExists = await _context.BikeDamages
            .AnyAsync(d => d.DamageId == damageId);

        if (!damageExists)
        {
            TempData["ErrorMessage"] = "Damage record not found. The damage record may not have been created properly.";
            ErrorMessage = "Damage record not found. The damage record may not have been created properly.";
            return RedirectToPage("/Renter/Damages");
        }

        // Then check if it belongs to this renter
        Damage = await _context.BikeDamages
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d => d.DamageId == damageId && d.RenterId == userId.Value);

        if (Damage == null)
        {
            // Check if damage exists but belongs to different renter
            var damageForOtherRenter = await _context.BikeDamages
                .FirstOrDefaultAsync(d => d.DamageId == damageId);
            
            if (damageForOtherRenter != null && damageForOtherRenter.RenterId != userId.Value)
            {
                TempData["ErrorMessage"] = "You do not have permission to access this damage record.";
                ErrorMessage = "You do not have permission to access this damage record.";
            }
            else
            {
                TempData["ErrorMessage"] = "Damage record not found or you do not have permission to access it.";
                ErrorMessage = "Damage record not found or you do not have permission to access it.";
            }
            return RedirectToPage("/Renter/Damages");
        }

        if (Damage.DamageStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This damage has already been paid or resolved.";
            ErrorMessage = "This damage has already been paid or resolved.";
            return RedirectToPage("/Renter/Damages");
        }

        if (PaymentMethodId <= 0)
        {
            ModelState.AddModelError(nameof(PaymentMethodId), "Please select a payment method.");
            ErrorMessage = "Please select a payment method.";
            return Page();
        }

        try
        {
            // For cash payments, mark as paid immediately
            if (PaymentMethodId == 4) // Cash
            {
                var success = await _bikeDamageService.MarkDamageAsPaidAsync(damageId, userId.Value, "Paid via cash");
                if (success)
                {
                    TempData["SuccessMessage"] = $"Damage charge of ₱{Damage.DamageCost:F2} has been marked as paid. Please coordinate with the owner for cash payment.";
                    return RedirectToPage("/Renter/Damages");
                }
            }
            else
            {
                // For gateway payments, create payment intent
                var gatewayResult = await _paymentService.CreateGatewayPaymentForDamageAsync(
                    damageId,
                    PaymentMethodId,
                    Damage.DamageCost
                );

                if (gatewayResult.success && !string.IsNullOrEmpty(gatewayResult.paymentIntentId))
                {
                    TempData["PaymentIntentId"] = gatewayResult.paymentIntentId;
                    TempData["DamageId"] = damageId.ToString();
                    TempData["ClientKey"] = gatewayResult.clientKey;
                    return RedirectToPage("/Renter/PayDamageGateway", new { 
                        damageId = damageId, 
                        paymentIntentId = gatewayResult.paymentIntentId,
                        paymentMethodId = PaymentMethodId
                    });
                }
                else
                {
                    TempData["ErrorMessage"] = gatewayResult.message ?? "Failed to process payment.";
                    ErrorMessage = gatewayResult.message ?? "Failed to process payment.";
                    return Page();
                }
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while processing payment. Please try again.";
            ErrorMessage = "An error occurred while processing payment. Please try again.";
            return Page();
        }

        // Set error/success messages from TempData if present
        if (TempData["ErrorMessage"] != null)
            ErrorMessage = TempData["ErrorMessage"].ToString();
        if (TempData["SuccessMessage"] != null)
            SuccessMessage = TempData["SuccessMessage"].ToString();

        return Page();
    }
}

