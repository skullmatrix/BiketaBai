using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace BiketaBai.Services;

public class SmsService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public SmsService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sends an SMS message using iProgSMS API
    /// </summary>
    /// <param name="phoneNumber">Phone number in local format (09123456789) or international format (+639123456789)</param>
    /// <param name="message">SMS message content</param>
    /// <returns>True if SMS was sent successfully, false otherwise</returns>
    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            var apiToken = _configuration["AppSettings:IprogSmsApiToken"];
            
            if (string.IsNullOrEmpty(apiToken))
            {
                Log.Warning("IPROG SMS API token not configured. Cannot send SMS.");
                return false;
            }

            // Convert phone number to international format (63XXXXXXXXX)
            var formattedPhone = FormatPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(formattedPhone))
            {
                Log.Warning("Invalid phone number format: {PhoneNumber}", phoneNumber);
                return false;
            }

            // Build API URL with query parameters
            var apiUrl = $"https://www.iprogsms.com/api/v1/sms_messages" +
                        $"?api_token={Uri.EscapeDataString(apiToken)}" +
                        $"&phone_number={Uri.EscapeDataString(formattedPhone)}" +
                        $"&message={Uri.EscapeDataString(message)}";

            Log.Information("Sending SMS via iProgSMS to {PhoneNumber} (formatted: {FormattedPhone})", 
                phoneNumber, formattedPhone);

            // Send POST request
            var response = await _httpClient.PostAsync(apiUrl, null);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Log.Information("SMS sent successfully to {PhoneNumber}. Response: {Response}", 
                    phoneNumber, responseContent);
                return true;
            }
            else
            {
                Log.Error("Failed to send SMS to {PhoneNumber}. Status: {StatusCode}, Response: {Response}", 
                    phoneNumber, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending SMS to {PhoneNumber}. Error: {ErrorMessage}", 
                phoneNumber, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends an OTP code via SMS
    /// </summary>
    /// <param name="phoneNumber">Phone number in local or international format</param>
    /// <param name="otpCode">6-digit OTP code</param>
    /// <returns>True if SMS was sent successfully</returns>
    public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
    {
        var message = $"Your Bike Ta Bai verification code is: {otpCode}. This code expires in 10 minutes. Do not share this code with anyone.";
        return await SendSmsAsync(phoneNumber, message);
    }

    /// <summary>
    /// Formats phone number to international format (63XXXXXXXXX)
    /// Accepts: 09123456789, +639123456789, 639123456789
    /// Returns: 639123456789 (no + sign, no leading 0)
    /// </summary>
    private string FormatPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        // Remove all non-digit characters except +
        var cleaned = phoneNumber.Trim().Replace(" ", "").Replace("-", "");

        // Handle +63 format
        if (cleaned.StartsWith("+63"))
        {
            cleaned = "63" + cleaned.Substring(3);
        }
        // Handle 0XXXXXXXXX format (local format)
        else if (cleaned.StartsWith("0") && cleaned.Length == 11)
        {
            cleaned = "63" + cleaned.Substring(1);
        }
        // Handle 63XXXXXXXXX format (already international)
        else if (cleaned.StartsWith("63") && cleaned.Length == 12)
        {
            // Already in correct format
        }
        // Handle 9XXXXXXXXX format (missing country code and leading 0)
        else if (cleaned.StartsWith("9") && cleaned.Length == 10)
        {
            cleaned = "63" + cleaned;
        }
        else
        {
            Log.Warning("Unexpected phone number format: {PhoneNumber}", phoneNumber);
            return string.Empty;
        }

        // Validate: should be 12 digits starting with 63
        if (cleaned.Length != 12 || !cleaned.StartsWith("63") || !Regex.IsMatch(cleaned, @"^63\d{10}$"))
        {
            Log.Warning("Invalid phone number format after cleaning: {PhoneNumber} -> {Cleaned}", 
                phoneNumber, cleaned);
            return string.Empty;
        }

        return cleaned;
    }
}

