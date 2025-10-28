using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ProfileModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ProfileModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public User CurrentAdmin { get; set; } = null!;
        
        // Statistics
        public int TotalActionsToday { get; set; }
        public int TotalActionsThisWeek { get; set; }
        public int TotalActionsThisMonth { get; set; }
        public DateTime LastLoginTime { get; set; }
        
        // System Info
        public int TotalSystemUsers { get; set; }
        public int TotalSystemBikes { get; set; }
        public int TotalSystemBookings { get; set; }
        public int PendingVerifications { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            CurrentAdmin = await _context.Users.FindAsync(userId);
            if (CurrentAdmin == null || !CurrentAdmin.IsAdmin)
            {
                return RedirectToPage("/AccessDenied");
            }

            // Load system statistics
            TotalSystemUsers = await _context.Users.CountAsync();
            TotalSystemBikes = await _context.Bikes.CountAsync();
            TotalSystemBookings = await _context.Bookings.CountAsync();
            PendingVerifications = await _context.Users.CountAsync(u => u.IsOwner && u.VerificationStatus == "Pending");

            // Simulate last login (in real app, this would be tracked in database)
            LastLoginTime = CurrentAdmin.UpdatedAt;

            // Simulate activity counts (in real app, these would come from an audit log table)
            TotalActionsToday = 0; // Placeholder
            TotalActionsThisWeek = 0; // Placeholder
            TotalActionsThisMonth = 0; // Placeholder

            // Populate input model
            Input.FullName = CurrentAdmin.FullName;
            Input.Phone = CurrentAdmin.Phone;
            Input.Address = CurrentAdmin.Address;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            CurrentAdmin = await _context.Users.FindAsync(userId);
            if (CurrentAdmin == null || !CurrentAdmin.IsAdmin)
            {
                return RedirectToPage("/AccessDenied");
            }

            if (string.IsNullOrWhiteSpace(Input.FullName) || 
                string.IsNullOrWhiteSpace(Input.Phone) || 
                string.IsNullOrWhiteSpace(Input.Address))
            {
                ErrorMessage = "All fields are required";
                return await OnGetAsync();
            }

            // Update admin profile
            CurrentAdmin.FullName = Input.FullName.Trim();
            CurrentAdmin.Phone = Input.Phone.Trim();
            CurrentAdmin.Address = Input.Address.Trim();
            CurrentAdmin.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = "Admin profile updated successfully!";
            return RedirectToPage();
        }
    }
}

