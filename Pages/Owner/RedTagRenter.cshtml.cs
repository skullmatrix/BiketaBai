using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;

namespace BiketaBai.Pages.Owner;

[Authorize]
public class RedTagRenterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly RenterRedTagService _redTagService;

    public RedTagRenterModel(BiketaBaiDbContext context, RenterRedTagService redTagService)
    {
        _context = context;
        _redTagService = redTagService;
    }

    public User? Renter { get; set; }
    public Booking? Booking { get; set; }
    public bool IsAlreadyRedTagged { get; set; }
    public List<BikeDamage> UnpaidDamages { get; set; } = new();

    [BindProperty]
    public string RedTagReason { get; set; } = string.Empty;

    [BindProperty]
    public string? RedTagDescription { get; set; }

    [BindProperty]
    public int? BookingId { get; set; }

    public async Task<IActionResult> OnGetAsync(int renterId, int? bookingId = null)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Renter = await _context.Users.FindAsync(renterId);
        if (Renter == null)
            return NotFound();

        // Check if already red-tagged
        IsAlreadyRedTagged = await _redTagService.IsRenterRedTaggedAsync(renterId);

        // If booking ID provided, get booking details
        if (bookingId.HasValue)
        {
            Booking = await _context.Bookings
                .Include(b => b.Bike)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId.Value && 
                                         b.Bike.OwnerId == userId.Value && 
                                         b.RenterId == renterId);
        }

        // Get unpaid damages for this renter
        var damageService = HttpContext.RequestServices.GetRequiredService<BikeDamageService>();
        UnpaidDamages = await damageService.GetDamagesForRenterAsync(renterId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int renterId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        if (!AuthHelper.IsOwner(User))
            return RedirectToPage("/Account/AccessDenied");

        Renter = await _context.Users.FindAsync(renterId);
        if (Renter == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(RedTagReason))
        {
            ModelState.AddModelError(nameof(RedTagReason), "Please select a reason for red tagging this renter.");
            IsAlreadyRedTagged = await _redTagService.IsRenterRedTaggedAsync(renterId);
            var damageService = HttpContext.RequestServices.GetRequiredService<BikeDamageService>();
            UnpaidDamages = await damageService.GetDamagesForRenterAsync(renterId);
            return Page();
        }

        var result = await _redTagService.RedTagRenterAsync(
            renterId,
            userId.Value,
            RedTagReason,
            RedTagDescription,
            BookingId
        );

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Dashboard/Owner");
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
            IsAlreadyRedTagged = await _redTagService.IsRenterRedTaggedAsync(renterId);
            var damageService = HttpContext.RequestServices.GetRequiredService<BikeDamageService>();
            UnpaidDamages = await damageService.GetDamagesForRenterAsync(renterId);
            return Page();
        }
    }
}

