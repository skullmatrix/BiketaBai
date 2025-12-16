using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Bookings;

public class ConfirmationModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public ConfirmationModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    public Booking? Booking { get; set; }

    public async Task<IActionResult> OnGetAsync(int bookingId)
    {
        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        Booking = await _context.Bookings
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.BikeType)
            .Include(b => b.Bike)
                .ThenInclude(bike => bike.Owner)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.RenterId == userId.Value);

        if (Booking == null)
            return NotFound();

        return Page();
    }
}

