using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Services;

public class WalletService
{
    private readonly BiketaBaiDbContext _context;

    public WalletService(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId);

        if (wallet == null)
        {
            wallet = new Wallet
            {
                UserId = userId,
                Balance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        return wallet;
    }

    public async Task<decimal> GetBalanceAsync(int userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return wallet.Balance;
    }

    public async Task<bool> LoadWalletAsync(int userId, decimal amount, string description, string? referenceId = null)
    {
        if (amount <= 0) return false;

        var wallet = await GetOrCreateWalletAsync(userId);
        var balanceBefore = wallet.Balance;
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new CreditTransaction
        {
            WalletId = wallet.WalletId,
            TransactionTypeId = 1, // Load
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = description,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CreditTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeductFromWalletAsync(int userId, decimal amount, int transactionTypeId, string description, string? referenceId = null)
    {
        if (amount <= 0) return false;

        var wallet = await GetOrCreateWalletAsync(userId);
        if (wallet.Balance < amount) return false;

        var balanceBefore = wallet.Balance;
        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new CreditTransaction
        {
            WalletId = wallet.WalletId,
            TransactionTypeId = transactionTypeId,
            Amount = -amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = description,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CreditTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddToWalletAsync(int userId, decimal amount, int transactionTypeId, string description, string? referenceId = null)
    {
        if (amount <= 0) return false;

        var wallet = await GetOrCreateWalletAsync(userId);
        var balanceBefore = wallet.Balance;
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new CreditTransaction
        {
            WalletId = wallet.WalletId,
            TransactionTypeId = transactionTypeId,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.Balance,
            Description = description,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.CreditTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> WithdrawFromWalletAsync(int userId, decimal amount, string description)
    {
        return await DeductFromWalletAsync(userId, amount, 2, description); // 2 = Withdrawal
    }

    public async Task<List<CreditTransaction>> GetTransactionHistoryAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        
        return await _context.CreditTransactions
            .Include(ct => ct.TransactionType)
            .Where(ct => ct.WalletId == wallet.WalletId)
            .OrderByDescending(ct => ct.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTransactionCountAsync(int userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return await _context.CreditTransactions
            .Where(ct => ct.WalletId == wallet.WalletId)
            .CountAsync();
    }
}

