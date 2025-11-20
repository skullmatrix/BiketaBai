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

    public async Task<bool> VerifyAddressExistsAsync(string address)
    {
        var result = await ValidateAddressAsync(address);
        return result.IsValid;
    }
}

