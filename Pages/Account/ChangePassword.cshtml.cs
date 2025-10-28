using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ChangePasswordModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Current password is required")]
            [DataType(DataType.Password)]
            [Display(Name = "Current Password")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "New password is required")]
            [StringLength(100, ErrorMessage = "The password must be at least {2} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your new password")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm New Password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmNewPassword { get; set; } = string.Empty;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors in the form";
                return Page();
            }

            // Get current user ID
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToPage("/Account/Login");
            }

            // Get user from database
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Verify current password
            if (!PasswordHelper.VerifyPassword(Input.CurrentPassword, user.PasswordHash))
            {
                ErrorMessage = "Current password is incorrect";
                return Page();
            }

            // Validate new password
            if (Input.NewPassword == Input.CurrentPassword)
            {
                ErrorMessage = "New password must be different from current password";
                return Page();
            }

            if (Input.NewPassword.Length < 8)
            {
                ErrorMessage = "New password must be at least 8 characters long";
                return Page();
            }

            try
            {
                // Hash and update new password
                user.PasswordHash = PasswordHelper.HashPassword(Input.NewPassword);
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                SuccessMessage = "Password changed successfully!";
                return RedirectToPage("/Account/Profile");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error changing password: {ex.Message}";
                return Page();
            }
        }
    }
}

