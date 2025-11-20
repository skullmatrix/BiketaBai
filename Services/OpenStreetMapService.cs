using System.Text.Json;

namespace BiketaBai.Services
{
    public class OpenStreetMapService
    {
        private readonly HttpClient _httpClient;

        public OpenStreetMapService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Set user agent as required by Nominatim usage policy
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BikeTaBai/1.0 (biketabai.net)");
        }

        public async Task<OSMAutocompleteResult> SearchAddressAsync(string query, string? sessionToken = null)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                return new OSMAutocompleteResult
                {
                    Success = false,
                    ErrorMessage = "Query must be at least 3 characters"
                };
            }

            try
            {
                // Use Nominatim Search API (free, no API key required)
                // Limit to Philippines and address types
                var url = "https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(query)}" +
                    "&format=json" +
                    "&addressdetails=1" +
                    "&limit=5" +
                    "&countrycodes=ph" +
                    "&dedupe=1";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new OSMAutocompleteResult
                    {
                        Success = false,
                        ErrorMessage = $"Request failed: {response.StatusCode}"
                    };
                }

                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var predictions = new List<OSMPlace>();

                    foreach (var item in root.EnumerateArray())
                    {
                        var displayName = item.TryGetProperty("display_name", out var displayNameElement)
                            ? displayNameElement.GetString() ?? ""
                            : "";

                        var placeId = item.TryGetProperty("place_id", out var placeIdElement)
                            ? placeIdElement.GetInt64().ToString()
                            : "";

                        // Extract main text and secondary text
                        var addressParts = displayName.Split(',');
                        var mainText = addressParts.Length > 0 ? addressParts[0].Trim() : displayName;
                        var secondaryText = addressParts.Length > 1 
                            ? string.Join(", ", addressParts.Skip(1).Take(2)).Trim()
                            : "";

                        predictions.Add(new OSMPlace
                        {
                            PlaceId = placeId,
                            DisplayName = displayName,
                            MainText = mainText,
                            SecondaryText = secondaryText,
                            Latitude = item.TryGetProperty("lat", out var latElement) 
                                ? double.TryParse(latElement.GetString(), out var lat) ? lat : 0 
                                : 0,
                            Longitude = item.TryGetProperty("lon", out var lonElement) 
                                ? double.TryParse(lonElement.GetString(), out var lon) ? lon : 0 
                                : 0
                        });
                    }

                    return new OSMAutocompleteResult
                    {
                        Success = true,
                        Predictions = predictions
                    };
                }
                else
                {
                    return new OSMAutocompleteResult
                    {
                        Success = true,
                        Predictions = new List<OSMPlace>(),
                        Status = "ZERO_RESULTS"
                    };
                }
            }
            catch (Exception ex)
            {
                return new OSMAutocompleteResult
                {
                    Success = false,
                    ErrorMessage = $"Error searching address: {ex.Message}"
                };
            }
        }

        public async Task<OSMGeocodeResult> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new OSMGeocodeResult
                {
                    Success = false,
                    ErrorMessage = "Address is required"
                };
            }

            try
            {
                var url = "https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(address)}" +
                    "&format=json" +
                    "&addressdetails=1" +
                    "&limit=1" +
                    "&countrycodes=ph";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new OSMGeocodeResult
                    {
                        Success = false,
                        ErrorMessage = $"Request failed: {response.StatusCode}"
                    };
                }

                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstResult = root.EnumerateArray().First();
                    var displayName = firstResult.TryGetProperty("display_name", out var displayNameElement)
                        ? displayNameElement.GetString() ?? ""
                        : "";

                    var lat = firstResult.TryGetProperty("lat", out var latElement)
                        ? (double.TryParse(latElement.GetString(), out var latitude) ? latitude : 0)
                        : 0;

                    var lon = firstResult.TryGetProperty("lon", out var lonElement)
                        ? (double.TryParse(lonElement.GetString(), out var longitude) ? longitude : 0)
                        : 0;

                    return new OSMGeocodeResult
                    {
                        Success = true,
                        FormattedAddress = displayName,
                        Latitude = lat,
                        Longitude = lon
                    };
                }
                else
                {
                    return new OSMGeocodeResult
                    {
                        Success = false,
                        ErrorMessage = "Address not found"
                    };
                }
            }
            catch (Exception ex)
            {
                return new OSMGeocodeResult
                {
                    Success = false,
                    ErrorMessage = $"Error geocoding address: {ex.Message}"
                };
            }
        }
    }

    public class OSMAutocompleteResult
    {
        public bool Success { get; set; }
        public List<OSMPlace> Predictions { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? Status { get; set; }
    }

    public class OSMPlace
    {
        public string PlaceId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string MainText { get; set; } = "";
        public string SecondaryText { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class OSMGeocodeResult
    {
        public bool Success { get; set; }
        public string? FormattedAddress { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

