using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Admin;

[Authorize(Roles = "Admin")]
public class VerifyOwnersModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public VerifyOwnersModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public List<User> PendingOwners { get; set; } = new();
    public int TotalPending { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
    public string? SearchQuery { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int page = 1, string? search = null)
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        CurrentPage = page;
        SearchQuery = search;

        // Build query with optional search
        var query = _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Pending" && !u.IsDeleted)
            .AsQueryable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            // Search in users and also in stores for store name
            var ownerIdsWithStoreMatch = await _context.Stores
                .Where(s => s.StoreName.Contains(search) && !s.IsDeleted)
                .Select(s => s.OwnerId)
                .ToListAsync();
            
            query = query.Where(u => 
                u.FullName.Contains(search) || 
                u.Email.Contains(search) || 
                (u.Phone != null && u.Phone.Contains(search)) ||
                ownerIdsWithStoreMatch.Contains(u.UserId));
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        // Get paginated results - load full entities (needed for all fields)
        PendingOwners = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Get statistics (cached counts for better performance)
        TotalPending = totalCount;
        TotalApproved = await _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Approved" && !u.IsDeleted)
            .CountAsync();
        TotalRejected = await _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Rejected" && !u.IsDeleted)
            .CountAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int userId)
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        if (!user.IsOwner)
        {
            ErrorMessage = "User is not an owner.";
            return RedirectToPage();
        }

        // Approve the owner
        user.VerificationStatus = "Approved";
        user.IsVerifiedOwner = true;
        user.VerificationDate = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SuccessMessage = $"Successfully approved {user.FullName} as a verified owner!";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int userId, string? rejectionReason)
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        if (!user.IsOwner)
        {
            ErrorMessage = "User is not an owner.";
            return RedirectToPage();
        }

        // Reject the owner
        user.VerificationStatus = "Rejected";
        user.IsVerifiedOwner = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SuccessMessage = $"Rejected verification for {user.FullName}.";
        return RedirectToPage();
    }
}

