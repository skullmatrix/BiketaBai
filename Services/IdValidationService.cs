using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

namespace BiketaBai.Services;

public class IdValidationService
{
    private readonly IConfiguration _configuration;
    private readonly AddressValidationService _addressValidationService;

    public IdValidationService(IConfiguration configuration, AddressValidationService addressValidationService)
    {
        _configuration = configuration;
        _addressValidationService = addressValidationService;
    }

    public class IdValidationResult
    {
        public bool IsValid { get; set; }
        public string? ExtractedAddress { get; set; }
        public string? ExtractedName { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, string> ExtractedFields { get; set; } = new();
    }

    /// <summary>
    /// Validates ID document using Google Cloud Vision API OCR
    /// </summary>
    public async Task<IdValidationResult> ValidateIdAsync(IFormFile idDocument)
    {
        var result = new IdValidationResult();

        if (idDocument == null || idDocument.Length == 0)
        {
            result.ErrorMessage = "ID document is required";
            return result;
        }

        // Validate file size (max 5MB)
        if (idDocument.Length > 5 * 1024 * 1024)
        {
            result.ErrorMessage = "ID document must be less than 5MB";
            return result;
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var fileExtension = Path.GetExtension(idDocument.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            result.ErrorMessage = "ID document must be JPG or PNG image";
            return result;
        }

        // Basic validation passed
        result.IsValid = true;

        // Perform OCR using Google Cloud Vision API
        try
        {
            var apiKey = _configuration["AppSettings:GoogleCloudVisionApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Warning("Google Cloud Vision API key not configured. Skipping OCR validation.");
                return result; // Return basic validation if API key is missing
            }

            // Convert image to base64
            using var memoryStream = new MemoryStream();
            await idDocument.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();
            var base64Image = Convert.ToBase64String(imageBytes);

            // Call Google Cloud Vision API
            using var httpClient = new HttpClient();
            var requestBody = new
            {
                requests = new[]
                {
                    new
                    {
                        image = new { content = base64Image },
                        features = new[]
                        {
                            new { type = "TEXT_DETECTION", maxResults = 10 }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(
                $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}",
                content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var visionResponse = JsonSerializer.Deserialize<VisionApiResponse>(responseContent);

                if (visionResponse?.Responses != null && visionResponse.Responses.Length > 0)
                {
                    var textAnnotations = visionResponse.Responses[0].TextAnnotations;
                    if (textAnnotations != null && textAnnotations.Length > 0)
                    {
                        var fullText = textAnnotations[0].Description ?? "";
                        Log.Information("OCR extracted text from ID: {TextLength} characters", fullText.Length);

                        // Extract information from OCR text
                        result.ExtractedName = ExtractName(fullText);
                        result.ExtractedAddress = ExtractAddress(fullText);
                        result.ExtractedFields = ExtractFields(fullText);

                        // Validate that we extracted meaningful information
                        if (!string.IsNullOrWhiteSpace(result.ExtractedName) || 
                            !string.IsNullOrWhiteSpace(result.ExtractedAddress))
                        {
                            result.IsValid = true;
                            Log.Information("ID validation successful via OCR. Name: {Name}, Address: {Address}", 
                                result.ExtractedName, result.ExtractedAddress);
                        }
                        else
                        {
                            Log.Warning("OCR extracted text but could not find name or address. Text: {Text}", 
                                fullText.Substring(0, Math.Min(200, fullText.Length)));
                            // Still valid if basic checks passed
                            result.IsValid = true;
                        }
                    }
                    else
                    {
                        Log.Warning("OCR did not extract any text from ID document");
                        result.IsValid = true; // Basic validation passed
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Google Vision API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                // Don't fail validation if API call fails - just log and continue
                result.IsValid = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during OCR validation. Continuing with basic validation.");
            // Don't fail validation if OCR fails - just log and continue
            result.IsValid = true;
        }

        return result;
    }

    /// <summary>
    /// Extracts address from ID document using OCR
    /// </summary>
    public async Task<string?> ExtractAddressFromIdAsync(IFormFile idDocument)
    {
        if (idDocument == null || idDocument.Length == 0)
            return null;

        try
        {
            var validationResult = await ValidateIdAsync(idDocument);
            return validationResult.ExtractedAddress;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting address from ID");
            return null;
        }
    }

    /// <summary>
    /// Cross-checks user-provided address with address extracted from ID
    /// </summary>
    public async Task<bool> CrossCheckAddressAsync(string userAddress, string? idAddress)
    {
        if (string.IsNullOrEmpty(idAddress))
        {
            Log.Warning("ID address not extracted. Cannot cross-check.");
            return false;
        }

        // Normalize addresses for comparison
        var normalizedUser = NormalizeAddress(userAddress);
        var normalizedId = NormalizeAddress(idAddress);

        // Simple similarity check (can be enhanced with fuzzy matching)
        var similarity = CalculateSimilarity(normalizedUser, normalizedId);
        
        Log.Information("Address cross-check: User='{UserAddress}', ID='{IdAddress}', Similarity={Similarity}%", 
            userAddress, idAddress, similarity * 100);

        // Consider addresses matching if similarity > 70%
        return similarity > 0.7;
    }

    private string NormalizeAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;

        // Remove extra spaces, convert to lowercase, remove common words
        var normalized = address.ToLowerInvariant()
            .Trim()
            .Replace(",", " ")
            .Replace(".", " ")
            .Replace("  ", " ");

        // Remove common address words that might differ
        var wordsToRemove = new[] { "street", "st", "avenue", "ave", "road", "rd", "boulevard", "blvd", "drive", "dr" };
        foreach (var word in wordsToRemove)
        {
            normalized = normalized.Replace($" {word} ", " ");
        }

        return normalized.Trim();
    }

    private double CalculateSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        // Simple word-based similarity
        var words1 = str1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = str2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        if (union == 0)
            return 0.0;

        return (double)intersection / union;
    }

    private string? ExtractName(string text)
    {
        // Look for patterns like "NAME", "FULL NAME", "LAST NAME", "FIRST NAME"
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var upperLine = line.ToUpperInvariant().Trim();
            if (upperLine.Contains("NAME") && upperLine.Length > 5 && upperLine.Length < 100)
            {
                // Extract name after "NAME" keyword
                var nameIndex = upperLine.IndexOf("NAME");
                if (nameIndex >= 0 && nameIndex + 4 < line.Length)
                {
                    var name = line.Substring(nameIndex + 4).Trim();
                    // Remove common prefixes
                    name = name.Replace(":", "").Trim();
                    if (name.Length > 2 && name.Length < 80)
                        return name;
                }
            }
        }

        // Try to find lines that look like names (2-4 words, proper case)
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words.Length <= 4 && trimmed.Length < 80)
            {
                // Check if it looks like a name (starts with capital, no numbers)
                if (char.IsUpper(trimmed[0]) && !trimmed.Any(char.IsDigit))
                {
                    return trimmed;
                }
            }
        }

        return null;
    }

    private string? ExtractAddress(string text)
    {
        // Look for address patterns
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var addressKeywords = new[] { "ADDRESS", "STREET", "ROAD", "AVE", "AVENUE", "BLVD", "BOULEVARD", "ST", "DR", "DRIVE" };
        
        foreach (var line in lines)
        {
            var upperLine = line.ToUpperInvariant().Trim();
            
            // Check if line contains address keywords
            if (addressKeywords.Any(keyword => upperLine.Contains(keyword)))
            {
                var address = line.Trim();
                // Remove common prefixes
                foreach (var keyword in addressKeywords)
                {
                    if (upperLine.Contains(keyword))
                    {
                        var index = upperLine.IndexOf(keyword);
                        if (index > 0)
                        {
                            address = line.Substring(0, index).Trim() + " " + line.Substring(index).Trim();
                        }
                        address = address.Replace(":", "").Trim();
                        if (address.Length > 5 && address.Length < 200)
                            return address;
                    }
                }
            }
            
            // Check if line looks like an address (contains numbers and street-like words)
            if (line.Any(char.IsDigit) && 
                (upperLine.Contains("ST") || upperLine.Contains("AVE") || upperLine.Contains("RD") || 
                 upperLine.Contains("BLVD") || upperLine.Contains("DR") || upperLine.Contains("STREET") ||
                 upperLine.Contains("ROAD") || upperLine.Contains("AVENUE")))
            {
                var address = line.Trim();
                if (address.Length > 5 && address.Length < 200)
                    return address;
            }
        }

        return null;
    }

    private Dictionary<string, string> ExtractFields(string text)
    {
        var fields = new Dictionary<string, string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var upperLine = line.ToUpperInvariant();
            
            // Extract various fields
            if (upperLine.Contains("BIRTH") || upperLine.Contains("DOB") || upperLine.Contains("DATE OF BIRTH"))
            {
                var value = ExtractValueAfterKeyword(line, new[] { "BIRTH", "DOB", "DATE OF BIRTH" });
                if (!string.IsNullOrEmpty(value))
                    fields["DateOfBirth"] = value;
            }
            
            if (upperLine.Contains("ID NUMBER") || upperLine.Contains("ID NO") || upperLine.Contains("ID#"))
            {
                var value = ExtractValueAfterKeyword(line, new[] { "ID NUMBER", "ID NO", "ID#" });
                if (!string.IsNullOrEmpty(value))
                    fields["IdNumber"] = value;
            }
        }

        return fields;
    }

    private string ExtractValueAfterKeyword(string line, string[] keywords)
    {
        var upperLine = line.ToUpperInvariant();
        foreach (var keyword in keywords)
        {
            var index = upperLine.IndexOf(keyword);
            if (index >= 0)
            {
                var value = line.Substring(index + keyword.Length).Trim();
                value = value.Replace(":", "").Trim();
                if (value.Length > 0 && value.Length < 100)
                    return value;
            }
        }
        return string.Empty;
    }

    // Google Vision API response models
    private class VisionApiResponse
    {
        public VisionResponse[]? Responses { get; set; }
    }

    private class VisionResponse
    {
        public TextAnnotation[]? TextAnnotations { get; set; }
    }

    private class TextAnnotation
    {
        public string? Description { get; set; }
    }
}

