using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account;

public class LoginModel : PageModel
{
    private readonly BiketaBaiDbContext _context;

    public LoginModel(BiketaBaiDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);

        if (user == null)
        {
            ErrorMessage = "Invalid email or password";
            return Page();
        }

        if (user.IsSuspended)
        {
            ErrorMessage = "Your account has been suspended. Please contact support.";
            return Page();
        }

        if (!user.IsEmailVerified)
        {
            ErrorMessage = "Please verify your email address before logging in. Check your inbox for the verification link.";
            return Page();
        }

        if (!PasswordHelper.VerifyPassword(Input.Password, user.PasswordHash))
        {
            ErrorMessage = "Invalid email or password";
            return Page();
        }

        // Update login tracking
        user.LastLoginAt = DateTime.UtcNow;
        user.LoginCount++;
        await _context.SaveChangesAsync();

        // Sign in user
        await AuthHelper.SignInUserAsync(HttpContext, user.UserId, user.Email, user.FullName, user.IsRenter, user.IsOwner, user.IsAdmin);

        // Redirect
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        
        if (user.IsAdmin)
            return RedirectToPage("/Admin/Dashboard");
        else if (user.IsOwner)
            return RedirectToPage("/Dashboard/Owner");
        else
            return RedirectToPage("/Dashboard/Renter");
    }
}

