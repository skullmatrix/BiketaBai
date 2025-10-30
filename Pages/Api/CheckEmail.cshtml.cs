using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using System.Text.Json;

namespace BiketaBai.Pages.Api
{
    public class CheckEmailModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public CheckEmailModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return new JsonResult(new { exists = false, message = "Email is required" })
                    {
                        StatusCode = 400
                    };
                }

                // Check if email exists in database
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower());

                return new JsonResult(new { exists = emailExists });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { exists = false, error = "An error occurred" })
                {
                    StatusCode = 500
                };
            }
        }
    }
}

