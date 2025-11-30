using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;
using Serilog;

namespace BiketaBai.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TestSmsModel : PageModel
{
    private readonly SmsService _smsService;
    private readonly OtpService _otpService;

    public TestSmsModel(SmsService smsService, OtpService otpService)
    {
        _smsService = smsService;
        _otpService = otpService;
    }

    [BindProperty]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    public string? Message { get; set; }

    [BindProperty]
    public string? OtpCode { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostSendSmsAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(Message))
        {
            ErrorMessage = "Phone number and message are required.";
            return Page();
        }

        try
        {
            var success = await _smsService.SendSmsAsync(PhoneNumber, Message);
            
            if (success)
            {
                SuccessMessage = $"SMS sent successfully to {PhoneNumber}!";
            }
            else
            {
                ErrorMessage = "Failed to send SMS. Please check the logs for details.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in TestSms page");
            ErrorMessage = $"Error: {ex.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSendOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            ErrorMessage = "Phone number is required.";
            return Page();
        }

        try
        {
            Log.Information("TestSms: Attempting to send OTP to {PhoneNumber}", PhoneNumber);
            var success = await _otpService.GenerateAndSendOtpAsync(PhoneNumber);
            
            if (success)
            {
                SuccessMessage = $"OTP code sent successfully to {PhoneNumber}! Check your phone for the 6-digit code.";
                Log.Information("TestSms: OTP sent successfully to {PhoneNumber}", PhoneNumber);
            }
            else
            {
                ErrorMessage = "Failed to send OTP. Please check the logs for details. Make sure the iProgSMS API token is configured correctly in appsettings.json.";
                Log.Warning("TestSms: Failed to send OTP to {PhoneNumber}", PhoneNumber);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in TestSms page when sending OTP to {PhoneNumber}", PhoneNumber);
            ErrorMessage = $"Error: {ex.Message}. Check the logs for more details.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostVerifyOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(OtpCode))
        {
            ErrorMessage = "Phone number and OTP code are required.";
            return Page();
        }

        try
        {
            var isValid = await _otpService.VerifyOtpAsync(PhoneNumber, OtpCode);
            
            if (isValid)
            {
                SuccessMessage = $"OTP verified successfully for {PhoneNumber}!";
            }
            else
            {
                ErrorMessage = "Invalid or expired OTP code. Please try again.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in TestSms page");
            ErrorMessage = $"Error: {ex.Message}";
        }

        return Page();
    }
}

