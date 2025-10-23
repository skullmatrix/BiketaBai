using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Wallet;

public class LoadWalletModel : PageModel
{
    private readonly WalletService _walletService;

    public LoadWalletModel(WalletService walletService)
    {
        _walletService = walletService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public decimal CurrentBalance { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Range(1, 1000000)]
        public decimal Amount { get; set; }

        [Required]
        public int PaymentMethodId { get; set; } = 2; // GCash
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        CurrentBalance = await _walletService.GetBalanceAsync(userId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        CurrentBalance = await _walletService.GetBalanceAsync(userId.Value);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Simulate payment processing
        var paymentMethod = Input.PaymentMethodId == 2 ? "GCash" : "QRPH";
        var reference = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        var success = await _walletService.LoadWalletAsync(
            userId.Value,
            Input.Amount,
            $"Wallet load via {paymentMethod}",
            reference
        );

        if (success)
        {
            TempData["SuccessMessage"] = $"Successfully loaded ₱{Input.Amount:F2} to your wallet via {paymentMethod}";
            return RedirectToPage("/Wallet/Index");
        }
        else
        {
            ErrorMessage = "Failed to load wallet. Please try again.";
            return Page();
        }
    }
}

