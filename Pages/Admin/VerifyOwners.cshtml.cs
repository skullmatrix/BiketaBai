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

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        // Get pending owner verifications
        PendingOwners = await _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Pending")
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        // Get statistics
        TotalPending = PendingOwners.Count;
        TotalApproved = await _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Approved")
            .CountAsync();
        TotalRejected = await _context.Users
            .Where(u => u.IsOwner && u.VerificationStatus == "Rejected")
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

