using BiketaBai.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BiketaBai.Pages.Api
{
    public class PlacesAutocompleteModel : PageModel
    {
        private readonly OpenStreetMapService _osmService;

        public PlacesAutocompleteModel(OpenStreetMapService osmService)
        {
            _osmService = osmService;
        }

        public async Task<IActionResult> OnGetAsync(string? query, string? sessionToken = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new JsonResult(new
                {
                    success = false,
                    error = "Query parameter is required"
                });
            }

            var result = await _osmService.SearchAddressAsync(query, sessionToken);

            if (result.Success)
            {
                return new JsonResult(new
                {
                    success = true,
                    predictions = result.Predictions.Select(p => new
                    {
                        place_id = p.PlaceId,
                        description = p.DisplayName,
                        main_text = p.MainText,
                        secondary_text = p.SecondaryText
                    })
                });
            }
            else
            {
                return new JsonResult(new
                {
                    success = false,
                    error = result.ErrorMessage ?? "Unknown error",
                    status = result.Status
                });
            }
        }
    }
}

