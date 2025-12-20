using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Renter;

public class PayDamageGatewayModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly PaymentService _paymentService;
    private readonly PaymentGatewayService _paymentGatewayService;
    private readonly BikeDamageService _bikeDamageService;

    public PayDamageGatewayModel(
        BiketaBaiDbContext context, 
        PaymentService paymentService,
        PaymentGatewayService paymentGatewayService,
        BikeDamageService bikeDamageService)
    {
        _context = context;
        _paymentService = paymentService;
        _paymentGatewayService = paymentGatewayService;
        _bikeDamageService = bikeDamageService;
    }

    public BikeDamage? Damage { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? ClientKey { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int? PaymentMethodId { get; set; }
    public bool IsProcessingPayment { get; set; }

    [BindProperty]
    public CardPaymentModel CardPayment { get; set; } = new();

    public class CardPaymentModel
    {
        public string CardholderName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int damageId, string? paymentIntentId, string? action, int? paymentMethodId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Damage = await _context.BikeDamages
            .Include(d => d.Bike)
            .Include(d => d.Booking)
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.DamageId == damageId && d.RenterId == userId.Value);

        if (Damage == null)
            return NotFound();

        if (Damage.DamageStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This damage has already been paid or resolved.";
            return RedirectToPage("/Renter/Damages");
        }

        PaymentIntentId = paymentIntentId ?? TempData["PaymentIntentId"]?.ToString();
        PaymentMethodId = paymentMethodId ?? (TempData["PaymentMethodId"] != null ? int.Parse(TempData["PaymentMethodId"].ToString()!) : null);
        ClientKey = TempData["ClientKey"]?.ToString();

        // Handle redirect from PayMongo after payment
        if (action == "confirm" || action == "check_status")
        {
            var intentId = Request.Query["payment_intent_id"].FirstOrDefault() 
                ?? Request.Query["payment_intent"].FirstOrDefault()
                ?? PaymentIntentId;
            
            if (!string.IsNullOrEmpty(intentId))
            {
                var statusResult = await _paymentGatewayService.GetPaymentIntentStatusAsync(intentId);
                
                if (statusResult.Success && statusResult.Status == "succeeded")
                {
                    // Mark damage as paid
                    var success = await _bikeDamageService.MarkDamageAsPaidAsync(damageId, userId.Value, "Paid via payment gateway");
                    
                    if (success)
                    {
                        TempData["SuccessMessage"] = "Damage charge paid successfully!";
                        return RedirectToPage("/Renter/Damages");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Payment succeeded but failed to update damage status.";
                    }
                }
            }
        }

        IsProcessingPayment = !string.IsNullOrEmpty(PaymentIntentId);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmPaymentAsync(int damageId, string paymentIntentId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var statusResult = await _paymentGatewayService.GetPaymentIntentStatusAsync(paymentIntentId);
        
        if (statusResult.Success && statusResult.Status == "succeeded")
        {
            var success = await _bikeDamageService.MarkDamageAsPaidAsync(damageId, userId.Value, "Paid via payment gateway");
            
            if (success)
            {
                TempData["SuccessMessage"] = "Damage charge paid successfully!";
                return RedirectToPage("/Renter/Damages");
            }
        }

        TempData["ErrorMessage"] = "Payment confirmation failed. Please try again.";
        return RedirectToPage(new { damageId = damageId, paymentIntentId = paymentIntentId, action = "check_status" });
    }
}

