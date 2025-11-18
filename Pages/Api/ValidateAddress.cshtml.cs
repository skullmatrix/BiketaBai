using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;

namespace BiketaBai.Pages.Api;

public class ValidateAddressModel : PageModel
{
    private readonly AddressValidationService _addressValidationService;

    public ValidateAddressModel(AddressValidationService addressValidationService)
    {
        _addressValidationService = addressValidationService;
    }

    public async Task<IActionResult> OnGetAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return new JsonResult(new { valid = false, error = "Address is required" });
        }

        var result = await _addressValidationService.ValidateAddressAsync(address);

        return new JsonResult(new
        {
            valid = result.IsValid,
            formattedAddress = result.FormattedAddress,
            latitude = result.Latitude,
            longitude = result.Longitude,
            error = result.ErrorMessage
        });
    }
}

