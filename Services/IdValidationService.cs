using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.RegularExpressions;

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
    /// Validates ID document (basic validation - file format and size)
    /// TODO: Integrate with OCR service (Google Cloud Vision API) for full validation
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
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var fileExtension = Path.GetExtension(idDocument.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            result.ErrorMessage = "ID document must be JPG, PNG, or PDF";
            return result;
        }

        // Basic validation passed
        result.IsValid = true;

        // TODO: Implement OCR using Google Cloud Vision API
        // For now, return basic validation
        Log.Information("ID document validated (basic): {FileName}, Size: {Size} bytes", 
            idDocument.FileName, idDocument.Length);

        return result;
    }

    /// <summary>
    /// Extracts address from ID document using OCR
    /// TODO: Implement with Google Cloud Vision API
    /// </summary>
    public async Task<string?> ExtractAddressFromIdAsync(IFormFile idDocument)
    {
        // TODO: Implement OCR extraction
        // 1. Upload image to Google Cloud Vision API
        // 2. Extract text using OCR
        // 3. Parse address from extracted text
        // 4. Return extracted address

        Log.Information("Address extraction from ID not yet implemented. Using placeholder.");
        return null;
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
}

