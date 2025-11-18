using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;

namespace BiketaBai.Services;

public class AddressValidationService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public AddressValidationService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _apiKey = _configuration["AppSettings:GoogleMapsApiKey"];
    }

    public class AddressValidationResult
    {
        public bool IsValid { get; set; }
        public string? StandardizedAddress { get; set; }
        public string? FormattedAddress { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public async Task<AddressValidationResult> ValidateAddressAsync(string address)
    {
        var result = new AddressValidationResult();

        if (string.IsNullOrWhiteSpace(address))
        {
            result.ErrorMessage = "Address is required";
            return result;
        }

        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_GOOGLE_MAPS_API_KEY_HERE")
        {
            Log.Warning("Google Maps API key not configured. Skipping address validation.");
            // Return valid but don't validate - allows registration to proceed
            result.IsValid = true;
            result.StandardizedAddress = address;
            result.FormattedAddress = address;
            return result;
        }

        try
        {
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={_apiKey}";

            Log.Information("Validating address via Google Maps API: {Address}", address);

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Google Maps API error: {StatusCode} - {Content}", response.StatusCode, content);
                result.ErrorMessage = "Address validation service unavailable";
                result.IsValid = false;
                return result;
            }

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            var status = root.GetProperty("status").GetString();

            if (status == "OK" && root.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var firstResult = results[0];
                var formattedAddress = firstResult.GetProperty("formatted_address").GetString();
                var geometry = firstResult.GetProperty("geometry");
                var location = geometry.GetProperty("location");
                var lat = location.GetProperty("lat").GetDouble();
                var lng = location.GetProperty("lng").GetDouble();

                result.IsValid = true;
                result.StandardizedAddress = formattedAddress;
                result.FormattedAddress = formattedAddress;
                result.Latitude = lat;
                result.Longitude = lng;

                Log.Information("Address validated successfully: {FormattedAddress}", formattedAddress);
            }
            else if (status == "ZERO_RESULTS")
            {
                result.IsValid = false;
                result.ErrorMessage = "Address not found. Please check and try again.";
                Log.Warning("Address not found: {Address}", address);
            }
            else
            {
                result.IsValid = false;
                result.ErrorMessage = $"Address validation failed: {status}";
                Log.Warning("Address validation failed with status: {Status}", status);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating address: {Address}", address);
            result.IsValid = false;
            result.ErrorMessage = "Error validating address. Please try again.";
        }

        return result;
    }

    public async Task<bool> VerifyAddressExistsAsync(string address)
    {
        var result = await ValidateAddressAsync(address);
        return result.IsValid;
    }
}

