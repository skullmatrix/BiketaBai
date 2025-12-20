using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Admin;

[Authorize]
public class RedTagsModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterRedTagService _redTagService;

    public RedTagsModel(BiketaBaiDbContext context, RenterRedTagService redTagService)
    {
        _context = context;
        _redTagService = redTagService;
    }

    public List<RenterRedTag> RedTags { get; set; } = new();
    public List<RenterRedTag> ActiveRedTags { get; set; } = new();
    public List<RenterRedTag> ResolvedRedTags { get; set; } = new();
    public string Filter { get; set; } = "active";

    [BindProperty]
    public ResolveRedTagInputModel ResolveInput { get; set; } = new();

    public class ResolveRedTagInputModel
    {
        [Required]
        public int RedTagId { get; set; }

        [MaxLength(500)]
        public string? ResolutionNotes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string filter = "active")
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        Filter = filter;

        var allRedTags = await _context.RenterRedTags
            .Include(rt => rt.Renter)
            .Include(rt => rt.Owner)
            .Include(rt => rt.Booking)
                .ThenInclude(b => b.Bike)
            .Include(rt => rt.Resolver)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync();

        ActiveRedTags = allRedTags.Where(rt => rt.IsActive).ToList();
        ResolvedRedTags = allRedTags.Where(rt => !rt.IsActive).ToList();

        RedTags = Filter == "active" ? ActiveRedTags : ResolvedRedTags;

        return Page();
    }

    public async Task<IActionResult> OnPostResolveAsync()
    {
        if (!AuthHelper.IsAdmin(User))
            return RedirectToPage("/Account/AccessDenied");

        var adminId = AuthHelper.GetCurrentUserId(User);
        if (!adminId.HasValue)
        {
            TempData["ErrorMessage"] = "User not authenticated.";
            return RedirectToPage();
        }

        var result = await _redTagService.ResolveRedTagAsync(
            ResolveInput.RedTagId,
            adminId.Value,
            ResolveInput.ResolutionNotes
        );

        if (result)
        {
            TempData["SuccessMessage"] = "Red tag has been resolved successfully. The renter can now log in again.";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to resolve red tag. It may have already been resolved or does not exist.";
        }

        return RedirectToPage(new { filter = Filter });
    }
}

