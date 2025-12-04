using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net.Http;

namespace BiketaBai.Services;

public class AddressValidationService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(1, 1); // Only 1 request at a time
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan _minRequestInterval = TimeSpan.FromSeconds(1.1); // Slightly more than 1 second to respect rate limit
    private static readonly ConcurrentDictionary<string, (AddressValidationResult Result, DateTime CachedAt)> _addressCache = new();
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30); // Cache addresses for 30 minutes

    public AddressValidationService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        // Set user agent as required by Nominatim usage policy
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BikeTaBai/1.0 (biketabai.net)");
        // Add Accept-Language header for better results
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en");
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

        // Check cache first
        var cacheKey = address.ToLowerInvariant().Trim();
        if (_addressCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.CachedAt < _cacheExpiry)
            {
                Log.Information("Address validation cache hit: {Address}", address);
                return cached.Result;
            }
            else
            {
                // Remove expired cache entry
                _addressCache.TryRemove(cacheKey, out _);
            }
        }

        // Rate limit: wait if needed to respect Nominatim's 1 request/second policy
        await _rateLimiter.WaitAsync();
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _minRequestInterval)
            {
                var delay = _minRequestInterval - timeSinceLastRequest;
                Log.Debug("Rate limiting: waiting {Delay}ms before next Nominatim request", delay.TotalMilliseconds);
                await Task.Delay(delay);
            }

            result = await ValidateAddressInternalAsync(address);
            _lastRequestTime = DateTime.UtcNow;

            // Cache successful results
            if (result.IsValid)
            {
                _addressCache.TryAdd(cacheKey, (result, DateTime.UtcNow));
            }
        }
        finally
        {
            _rateLimiter.Release();
        }

        return result;
    }

    private async Task<AddressValidationResult> ValidateAddressInternalAsync(string address, int retryCount = 0)
    {
        var result = new AddressValidationResult();
        const int maxRetries = 3;

        try
        {
            // Use OpenStreetMap Nominatim API (free, no API key required)
            var encodedAddress = Uri.EscapeDataString(address);
            var url = $"https://nominatim.openstreetmap.org/search?q={encodedAddress}&format=json&addressdetails=1&limit=1&countrycodes=ph";

            Log.Information("Validating address via OpenStreetMap Nominatim: {Address} (attempt {Attempt})", address, retryCount + 1);

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // Handle rate limiting (429 Too Many Requests)
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
            {
                if (retryCount < maxRetries)
                {
                    // Exponential backoff: wait 2^retryCount seconds
                    var delaySeconds = Math.Pow(2, retryCount);
                    Log.Warning("Rate limited by Nominatim API. Retrying after {Delay} seconds (attempt {Attempt}/{MaxRetries})", 
                        delaySeconds, retryCount + 1, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    return await ValidateAddressInternalAsync(address, retryCount + 1);
                }
                else
                {
                    Log.Error("Rate limited by Nominatim API after {MaxRetries} retries", maxRetries);
                    result.ErrorMessage = "Address validation service is temporarily busy. Please try again in a moment.";
                    result.IsValid = false;
                    return result;
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("OpenStreetMap API error: {StatusCode} - {Content}", response.StatusCode, content);
                result.ErrorMessage = "Address validation service unavailable. Please try again.";
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
                // Address not found in search results
                // Try one more search without country restriction to be more lenient
                Log.Information("Address not found with country restriction. Trying broader search for: {Address}", address);
                try
                {
                    var broaderUrl = $"https://nominatim.openstreetmap.org/search?q={encodedAddress}&format=json&addressdetails=1&limit=1";
                    var broaderResponse = await _httpClient.GetAsync(broaderUrl);
                    
                    if (broaderResponse.IsSuccessStatusCode)
                    {
                        var broaderContent = await broaderResponse.Content.ReadAsStringAsync();
                        var broaderJsonDoc = JsonDocument.Parse(broaderContent);
                        var broaderRoot = broaderJsonDoc.RootElement;
                        
                        if (broaderRoot.ValueKind == JsonValueKind.Array && broaderRoot.GetArrayLength() > 0)
                        {
                            var firstResult = broaderRoot[0];
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

                            Log.Information("Address validated successfully with broader search: {FormattedAddress}", displayName);
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Broader address search failed for: {Address}", address);
                }
                
                // If still not found, but address seems reasonable (at least 10 characters), accept it
                // This prevents blocking valid registrations due to API limitations
                if (address.Length >= 10)
                {
                    Log.Warning("Address not found in validation service but accepting as valid (length check): {Address}", address);
                    result.IsValid = true;
                    result.StandardizedAddress = address;
                    result.FormattedAddress = address;
                    // No coordinates since we couldn't find the address
                }
                else
                {
                result.IsValid = false;
                    result.ErrorMessage = "Address not found. Please select an address from the suggestions or provide a more detailed address.";
                    Log.Warning("Address not found and too short: {Address}", address);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            // Network errors - retry if we haven't exceeded max retries
            if (retryCount < maxRetries)
            {
                var delaySeconds = Math.Pow(2, retryCount);
                Log.Warning("Network error during address validation. Retrying after {Delay} seconds: {Error}", 
                    delaySeconds, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                return await ValidateAddressInternalAsync(address, retryCount + 1);
            }
            else
            {
                Log.Error(ex, "Network error validating address after {MaxRetries} retries: {Address}", maxRetries, address);
                result.IsValid = false;
                result.ErrorMessage = "Network error validating address. Please check your connection and try again.";
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
    // When coordinates are provided, we can be more lenient - if coordinates are valid, we trust them
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

        // If coordinates are provided and valid, we can trust them from autocomplete
        // This reduces API calls and avoids rate limiting issues
        // We'll do a quick reverse geocoding to get the standardized address, but if it fails, we'll accept the coordinates
        try
        {
            // Check cache first (using coordinates as key)
            var cacheKey = $"coord_{latitude:F6}_{longitude:F6}";
            if (_addressCache.TryGetValue(cacheKey, out var cached))
            {
                if (DateTime.UtcNow - cached.CachedAt < _cacheExpiry)
                {
                    Log.Information("Coordinate validation cache hit: {Latitude}, {Longitude}", latitude, longitude);
                    return cached.Result;
                }
                else
                {
                    _addressCache.TryRemove(cacheKey, out _);
                }
            }

            // Rate limit: wait if needed
            await _rateLimiter.WaitAsync();
            try
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLastRequest < _minRequestInterval)
                {
                    var delay = _minRequestInterval - timeSinceLastRequest;
                    await Task.Delay(delay);
                }

                // Use reverse geocoding with provided coordinates
                // This is more reliable when coordinates come from autocomplete
                var url = $"https://nominatim.openstreetmap.org/reverse?lat={latitude}&lon={longitude}&format=json&addressdetails=1";

                Log.Information("Validating address via reverse geocoding: {Address} at {Latitude}, {Longitude}", address, latitude, longitude);

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                // Handle rate limiting
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    Log.Warning("Rate limited during reverse geocoding. Accepting coordinates as valid since they came from autocomplete.");
                    // Since coordinates came from autocomplete, we trust them even if API is rate limited
                    result.IsValid = true;
                    result.StandardizedAddress = address; // Use provided address
                    result.FormattedAddress = address;
                    result.Latitude = latitude;
                    result.Longitude = longitude;
                    _lastRequestTime = DateTime.UtcNow;
                    _addressCache.TryAdd(cacheKey, (result, DateTime.UtcNow));
                    return result;
                }

                if (response.IsSuccessStatusCode)
                {
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
                            _lastRequestTime = DateTime.UtcNow;
                            _addressCache.TryAdd(cacheKey, (result, DateTime.UtcNow));
                            return result;
                        }
                    }
                }

                // If reverse geocoding didn't work but we have valid coordinates from autocomplete,
                // we trust them to avoid blocking registration due to API issues
                Log.Information("Reverse geocoding returned empty result, but accepting coordinates from autocomplete as valid.");
                result.IsValid = true;
                result.StandardizedAddress = address;
                result.FormattedAddress = address;
                result.Latitude = latitude;
                result.Longitude = longitude;
                _lastRequestTime = DateTime.UtcNow;
                _addressCache.TryAdd(cacheKey, (result, DateTime.UtcNow));
                return result;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in reverse geocoding for address: {Address}. Accepting coordinates as valid since they came from autocomplete.", address);
            // Since coordinates came from autocomplete, we trust them even if API call fails
            result.IsValid = true;
            result.StandardizedAddress = address;
            result.FormattedAddress = address;
            result.Latitude = latitude;
            result.Longitude = longitude;
            return result;
        }
    }

    public async Task<bool> VerifyAddressExistsAsync(string address)
    {
        var result = await ValidateAddressAsync(address);
        return result.IsValid;
    }
}

