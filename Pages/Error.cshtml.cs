using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace BiketaBai.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public string? RequestId { get; set; }
    public string? ErrorMessage { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        // Try to get error details from exception handler
        var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature?.Error != null)
        {
            var exception = exceptionHandlerPathFeature.Error;
            _logger.LogError(exception, "Error page accessed. RequestId: {RequestId}", RequestId);
            
            // Only show detailed error in development
            if (HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                ErrorMessage = exception.ToString();
            }
            else
            {
                // In production, log but don't expose details
                ErrorMessage = "An error occurred. Please check the application logs for details.";
            }
        }
    }
}

