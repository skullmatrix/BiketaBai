using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Models;
using BiketaBai.Services;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Wallet;

public class WalletIndexModel : PageModel
{
    private readonly WalletService _walletService;

    public WalletIndexModel(WalletService walletService)
    {
        _walletService = walletService;
    }

    public decimal WalletBalance { get; set; }
    public decimal TotalLoaded { get; set; }
    public decimal TotalSpent { get; set; }
    public List<CreditTransaction> Transactions { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;

    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        CurrentPage = page;

        WalletBalance = await _walletService.GetBalanceAsync(userId.Value);
        Transactions = await _walletService.GetTransactionHistoryAsync(userId.Value, CurrentPage, PageSize);
        
        var totalCount = await _walletService.GetTransactionCountAsync(userId.Value);
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Calculate totals
        var allTransactions = await _walletService.GetTransactionHistoryAsync(userId.Value, 1, int.MaxValue);
        TotalLoaded = allTransactions
            .Where(t => t.TransactionTypeId == 1) // Load
            .Sum(t => t.Amount);
        
        TotalSpent = Math.Abs(allTransactions
            .Where(t => t.TransactionTypeId == 3) // Rental Payment
            .Sum(t => t.Amount));

        return Page();
    }
}

