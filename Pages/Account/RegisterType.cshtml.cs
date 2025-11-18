using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BiketaBai.Pages.Account;

public class RegisterTypeModel : PageModel
{
    [BindProperty]
    public string? SelectedType { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrEmpty(SelectedType))
        {
            ModelState.AddModelError("SelectedType", "Please select a user type");
            return Page();
        }

        // Store selection in TempData for next step
        TempData["RegistrationType"] = SelectedType;

        // Redirect to appropriate registration page immediately
        if (SelectedType == "Renter")
        {
            return RedirectToPage("/Account/RegisterRenter");
        }
        else if (SelectedType == "Owner")
        {
            return RedirectToPage("/Account/RegisterOwner");
        }

        ModelState.AddModelError("SelectedType", "Invalid user type selected");
        return Page();
    }
}

