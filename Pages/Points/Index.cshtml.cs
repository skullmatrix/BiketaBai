using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Points;

public class PointsIndexModel : PageModel
{
    private readonly PointsService _pointsService;
    private readonly WalletService _walletService;

    public PointsIndexModel(PointsService pointsService, WalletService walletService)
    {
        _pointsService = pointsService;
        _walletService = walletService;
    }

    public int PointsBalance { get; set; }
    public List<PointsHistory> PointsHistory { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        PointsBalance = await _pointsService.GetPointsBalanceAsync(userId.Value);
        PointsHistory = await _pointsService.GetPointsHistoryAsync(userId.Value, 1, 50);

        return Page();
    }

    public async Task<IActionResult> OnPostRedeemAsync(int pointsToRedeem)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (pointsToRedeem < 100 || pointsToRedeem % 100 != 0)
        {
            TempData["ErrorMessage"] = "Please redeem in multiples of 100 points";
            return RedirectToPage();
        }

        PointsBalance = await _pointsService.GetPointsBalanceAsync(userId.Value);
        
        if (pointsToRedeem > PointsBalance)
        {
            TempData["ErrorMessage"] = "Insufficient points balance";
            return RedirectToPage();
        }

        // Redeem points
        var credits = await _pointsService.ConvertPointsToCredits(pointsToRedeem);
        var redeemSuccess = await _pointsService.RedeemPointsAsync(
            userId.Value,
            pointsToRedeem,
            $"Redeemed for ₱{credits:F2} wallet credit"
        );

        if (redeemSuccess)
        {
            // Add credits to wallet
            await _walletService.AddToWalletAsync(
                userId.Value,
                credits,
                1, // Load type
                $"Points redemption: {pointsToRedeem} points",
                $"Points-{DateTime.UtcNow.Ticks}"
            );

            TempData["SuccessMessage"] = $"Successfully redeemed {pointsToRedeem} points for ₱{credits:F2}!";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to redeem points. Please try again.";
        }

        return RedirectToPage();
    }
}

