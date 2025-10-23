using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Helpers;

namespace BiketaBai.Pages.Account;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await AuthHelper.SignOutUserAsync(HttpContext);
        return RedirectToPage("/Index");
    }
}

