using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;

namespace BiketaBai.Services;

public class AddressValidationService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AddressValidationService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        // Set user agent as required by Nominatim usage policy
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BikeTaBai/1.0 (biketabai.net)");
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

        try
        {
            // Use OpenStreetMap Nominatim API (free, no API key required)
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://nominatim.openstreetmap.org/search?q={encodedAddress}&format=json&addressdetails=1&limit=1&countrycodes=ph";

            Log.Information("Validating address via OpenStreetMap Nominatim: {Address}", address);

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("OpenStreetMap API error: {StatusCode} - {Content}", response.StatusCode, content);
                result.ErrorMessage = "Address validation service unavailable";
                result.IsValid = false;
                return result;
            }

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstResult = root[0];
                var displayName = firstResult.TryGetProperty("display_name", out var displayNameElement)
                    ? displayNameElement.GetString() ?? ""
                    : "";

                var lat = firstResult.TryGetProperty("lat", out var latElement)
                    ? (double.TryParse(latElement.GetString(), out var latitude) ? latitude : 0)
                    : 0;

                var lng = firstResult.TryGetProperty("lon", out var lonElement)
                    ? (double.TryParse(lonElement.GetString(), out var longitude) ? longitude : 0)
                    : 0;

                result.IsValid = true;
                result.StandardizedAddress = displayName;
                result.FormattedAddress = displayName;
                result.Latitude = lat;
                result.Longitude = lng;

                Log.Information("Address validated successfully: {FormattedAddress}", displayName);
            }
            else
            {
                result.IsValid = false;
                result.ErrorMessage = "Address not found. Please check and try again.";
                Log.Warning("Address not found: {Address}", address);
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

    // Overload that accepts coordinates from autocomplete selection
    public async Task<AddressValidationResult> ValidateAddressAsync(string address, double latitude, double longitude)
    {
        var result = new AddressValidationResult();

        if (string.IsNullOrWhiteSpace(address))
        {
            result.ErrorMessage = "Address is required";
            return result;
        }

        // Validate coordinates are within valid range
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            Log.Warning("Invalid coordinates provided: {Latitude}, {Longitude}. Falling back to address search.", latitude, longitude);
            // Fall back to regular validation
            return await ValidateAddressAsync(address);
        }

        try
        {
            // Use reverse geocoding with provided coordinates
            // This is more reliable when coordinates come from autocomplete
            var url = $"https://nominatim.openstreetmap.org/reverse?lat={latitude}&lon={longitude}&format=json&addressdetails=1";

            Log.Information("Validating address via reverse geocoding: {Address} at {Latitude}, {Longitude}", address, latitude, longitude);

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Reverse geocoding failed: {StatusCode}. Falling back to address search.", response.StatusCode);
                // Fall back to regular validation
                return await ValidateAddressAsync(address);
            }

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var displayName = root.TryGetProperty("display_name", out var displayNameElement)
                    ? displayNameElement.GetString() ?? ""
                    : "";

                // If we got a valid response, use it
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    result.IsValid = true;
                    result.StandardizedAddress = displayName;
                    result.FormattedAddress = displayName;
                    result.Latitude = latitude;
                    result.Longitude = longitude;

                    Log.Information("Address validated successfully via reverse geocoding: {FormattedAddress}", displayName);
                    return result;
                }
            }

            // If reverse geocoding didn't work, fall back to regular validation
            Log.Information("Reverse geocoding returned empty result. Falling back to address search.");
            return await ValidateAddressAsync(address);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in reverse geocoding for address: {Address}. Falling back to address search.", address);
            // Fall back to regular validation
            return await ValidateAddressAsync(address);
        }
    }

    public async Task<bool> VerifyAddressExistsAsync(string address)
    {
        var result = await ValidateAddressAsync(address);
        return result.IsValid;
    }
}

