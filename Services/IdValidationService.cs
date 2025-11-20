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

        // Note: File size validation removed - images are auto-compressed
        // No size limit check here

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var fileExtension = Path.GetExtension(idDocument.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            result.ErrorMessage = "ID document must be JPG or PNG image";
            result.IsValid = false;
            return result;
        }

        // Default to false - will be set to true only if OCR verification passes
        result.IsValid = false;

        // Perform OCR using Google Cloud Vision API
        try
        {
            var apiKey = _configuration["AppSettings:GoogleCloudVisionApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Log.Warning("Google Cloud Vision API key not configured. Cannot verify ID.");
                result.IsValid = false;
                result.ErrorMessage = "Please upload a valid ID";
                return result;
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

                if (visionResponse?.Responses != null && visionResponse.Responses.Length > 0 && 
                    visionResponse.Responses[0] != null)
                {
                    var textAnnotations = visionResponse.Responses[0].TextAnnotations;
                    if (textAnnotations != null && textAnnotations.Length > 0)
                    {
                        var fullText = textAnnotations[0].Description ?? "";
                        Log.Information("OCR extracted text from ID: {TextLength} characters. First 200 chars: {TextPreview}", 
                            fullText.Length, fullText.Substring(0, Math.Min(200, fullText.Length)));

                        // Extract information from OCR text
                        result.ExtractedName = ExtractName(fullText);
                        result.ExtractedAddress = ExtractAddress(fullText);
                        result.ExtractedFields = ExtractFields(fullText);

                        // Verify if this looks like a real ID document
                        var verificationScore = VerifyIdDocument(fullText, result.ExtractedFields);
                        
                        // Log the verification score and extracted text snippet for debugging
                        Log.Information("ID verification score: {Score}/5, Text length: {Length}, Extracted name: {Name}, Extracted address: {Address}", 
                            verificationScore, fullText.Length, result.ExtractedName ?? "none", result.ExtractedAddress ?? "none");
                        Log.Debug("Full OCR text (first 500 chars): {FullText}", fullText.Substring(0, Math.Min(500, fullText.Length)));
                        
                        if (verificationScore >= 4) // Very high confidence it's a real ID
                        {
                            result.IsValid = true;
                            Log.Information("ID validation successful via OCR. Name: {Name}, Address: {Address}, Verification Score: {Score}/5", 
                                result.ExtractedName, result.ExtractedAddress, verificationScore);
                        }
                        else if (verificationScore >= 3) // High confidence
                        {
                            result.IsValid = true;
                            Log.Information("ID validation successful via OCR. Verification Score: {Score}/5", verificationScore);
                        }
                        else if (verificationScore >= 2) // Medium confidence - require at least name or address
                        {
                            if (!string.IsNullOrWhiteSpace(result.ExtractedName) || 
                                !string.IsNullOrWhiteSpace(result.ExtractedAddress))
                            {
                                result.IsValid = true;
                                Log.Warning("ID validation passed with medium confidence (Score: {Score}/5). Manual review recommended.", verificationScore);
                            }
                            else
                            {
                                // Medium score but no name/address extracted - check if text looks reasonable
                                // If text is substantial and contains typical ID elements, accept it
                                if (fullText.Length > 50 && HasIdLikeContent(fullText))
                                {
                                    result.IsValid = true;
                                    Log.Warning("ID validation passed with medium score ({Score}/5) but no name/address extracted. Text appears ID-like.", verificationScore);
                                }
                                else
                                {
                                    result.IsValid = false;
                                    result.ErrorMessage = "Please upload a valid ID";
                                    Log.Warning("ID validation failed. Medium score ({Score}/5) but could not extract name or address and text doesn't appear ID-like.", verificationScore);
                                }
                            }
                        }
                        else if (verificationScore >= 1) // Low confidence but has some ID-like elements
                        {
                            // If text is substantial and contains typical ID elements, accept it
                            if (fullText.Length > 100 && HasIdLikeContent(fullText))
                            {
                                result.IsValid = true;
                                Log.Warning("ID validation passed with low score ({Score}/5) but text appears ID-like. Manual review recommended.", verificationScore);
                            }
                            else
                            {
                                result.IsValid = false;
                                result.ErrorMessage = "Please upload a valid ID";
                                Log.Warning("ID validation failed. Low verification score ({Score}/5). Text: {Text}", 
                                    verificationScore, fullText.Substring(0, Math.Min(200, fullText.Length)));
                            }
                        }
                        else
                        {
                            // Very low confidence - not a valid ID
                            result.IsValid = false;
                            result.ErrorMessage = "Please upload a valid ID";
                            Log.Warning("ID validation failed. Very low verification score ({Score}/5). Text: {Text}", 
                                verificationScore, fullText.Substring(0, Math.Min(200, fullText.Length)));
                        }
                    }
                    else
                    {
                        Log.Warning("OCR did not extract any text from ID document");
                        // No text extracted - likely not a valid ID document
                        result.IsValid = false;
                        result.ErrorMessage = "Please upload a valid ID";
                    }
                }
                else
                {
                    // Vision API returned empty or null response
                    Log.Warning("Vision API returned empty or null response");
                    result.IsValid = false;
                    result.ErrorMessage = "Please upload a valid ID";
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Google Vision API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                // API call failed - cannot verify ID, reject it
                result.IsValid = false;
                result.ErrorMessage = "Please upload a valid ID";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during OCR validation. Cannot verify ID authenticity.");
            // OCR validation failed - cannot verify if it's a real ID
            result.IsValid = false;
            result.ErrorMessage = "Please upload a valid ID";
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

    /// <summary>
    /// Verifies if the extracted text looks like a real ID document
    /// Returns a score from 0-5 indicating confidence level
    /// </summary>
    private int VerifyIdDocument(string fullText, Dictionary<string, string> extractedFields)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return 0;

        int score = 0;
        var upperText = fullText.ToUpperInvariant();

        // Check 1: Contains ID number or identification number (1 point)
        var idNumberPatterns = new[] { 
            "ID NO", "ID NUMBER", "ID#", "ID:", "IDENTIFICATION", 
            "LICENSE NO", "LICENSE NUMBER", "LICENSE#", 
            "PASSPORT NO", "PASSPORT NUMBER", "PASSPORT#",
            "SSS NO", "SSS NUMBER", "SSS#",
            "TIN NO", "TIN NUMBER", "TIN#",
            "DRIVER", "DRIVER'S", "DRIVERS"
        };
        if (idNumberPatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score++;
            Log.Debug("ID verification: Found ID number pattern (+1 point)");
        }

        // Check 2: Contains date of birth or birth date (1 point)
        var dobPatterns = new[] { 
            "DATE OF BIRTH", "DOB", "BIRTH DATE", "BIRTHDATE", 
            "BORN", "BIRTH", "MM/DD/YYYY", "DD/MM/YYYY", 
            "YYYY-MM-DD"
        };
        if (dobPatterns.Any(pattern => upperText.Contains(pattern)) || 
            extractedFields.ContainsKey("DateOfBirth"))
        {
            score++;
            Log.Debug("ID verification: Found date of birth (+1 point)");
        }

        // Check 3: Contains address field (1 point)
        var addressPatterns = new[] { 
            "ADDRESS", "RESIDENCE", "HOME ADDRESS", "PERMANENT ADDRESS",
            "STREET", "BARANGAY", "CITY", "PROVINCE"
        };
        if (addressPatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score++;
            Log.Debug("ID verification: Found address field (+1 point)");
        }

        // Check 4: Contains expiration date or valid until (1 point)
        var expiryPatterns = new[] { 
            "EXPIR", "EXPIRES", "VALID UNTIL", "VALID THRU", 
            "VALID UNTIL", "EFFECTIVE", "ISSUED", "VALIDITY",
            "MM/DD/YYYY", "DD/MM/YYYY"
        };
        if (expiryPatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score++;
            Log.Debug("ID verification: Found expiration/validity date (+1 point)");
        }

        // Check 5: Contains name field and name looks valid (1 point)
        var namePatterns = new[] { 
            "NAME", "FULL NAME", "LAST NAME", "FIRST NAME", 
            "GIVEN NAME", "SURNAME", "FAMILY NAME"
        };
        if (namePatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score++;
            Log.Debug("ID verification: Found name field (+1 point)");
        }

        // Bonus: Check for government agency keywords (Philippines-specific)
        var agencyPatterns = new[] { 
            "PHILIPPINES", "REPUBLIC", "LTO", "NBI", "SSS", 
            "PHILHEALTH", "PAG-IBIG", "TIN", "CIVIL", "COMMISSION",
            "DEPARTMENT", "NATIONAL", "PHILIPPINE"
        };
        if (agencyPatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score++; // Bonus point for government-issued document
            Log.Debug("ID verification: Found government agency keyword (+1 bonus point)");
        }

        // Verify structure: Should have at least 3-4 lines of text for a proper ID
        var lines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var meaningfulLines = lines.Count(line => line.Trim().Length > 5);
        if (meaningfulLines >= 3)
        {
            // Already counted in other checks, but verify minimum structure
            Log.Debug("ID verification: Document has {LineCount} meaningful lines of text", meaningfulLines);
        }
        else if (meaningfulLines < 2)
        {
            // Too little text - definitely not a valid ID
            score = 0;
            Log.Warning("ID verification: Document has very little text ({LineCount} lines). Not a valid ID.", meaningfulLines);
            return 0; // Return immediately - invalid ID
        }

        // Check for suspicious patterns - reject immediately if found
        var suspiciousPatterns = new[] { 
            "SAMPLE", "EXAMPLE", "TEST", "DEMO", "PLACEHOLDER",
            "FAKE", "MOCK", "DRAFT", "TEMPLATE"
        };
        if (suspiciousPatterns.Any(pattern => upperText.Contains(pattern)))
        {
            score = 0; // Reject immediately if placeholder/sample text found
            Log.Warning("ID verification: Found suspicious placeholder/sample patterns. ID rejected.");
            return 0; // Return immediately - invalid ID
        }

        // Check for placeholder ID numbers (like XXXX, 12345, etc.)
        var placeholderIdPatterns = new Regex(@"\b(XXXX+|12345+|00000+|11111+|12345678|99999999)\b", RegexOptions.IgnoreCase);
        if (placeholderIdPatterns.IsMatch(fullText))
        {
            score = 0; // Reject if placeholder ID number found
            Log.Warning("ID verification: Found placeholder ID number pattern. ID rejected.");
            return 0; // Return immediately - invalid ID
        }

        return Math.Min(5, Math.Max(0, score)); // Clamp between 0-5
    }

    /// <summary>
    /// Checks if the text has ID-like content (contains numbers, dates, etc.)
    /// </summary>
    private bool HasIdLikeContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var upperText = text.ToUpperInvariant();
        
        // Check for common ID-like patterns
        // Has numbers (likely ID numbers, dates)
        bool hasNumbers = text.Any(char.IsDigit);
        
        // Has dates (MM/DD/YYYY, DD/MM/YYYY, or YYYY-MM-DD patterns)
        bool hasDates = Regex.IsMatch(text, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}");
        
        // Has letters (likely names, addresses)
        bool hasLetters = text.Any(char.IsLetter);
        
        // Has common ID words
        var idWords = new[] { "REPUBLIC", "PHILIPPINES", "GOVERNMENT", "IDENTIFICATION", "LICENSE", "PASSPORT" };
        bool hasIdWords = idWords.Any(word => upperText.Contains(word));
        
        // Has typical ID field indicators
        var fieldIndicators = new[] { ":", "-", "NO", "NUMBER", "DATE", "BIRTH", "ADDRESS" };
        bool hasFieldIndicators = fieldIndicators.Any(indicator => upperText.Contains(indicator));
        
        // Consider it ID-like if it has numbers and letters, and either dates or ID words or field indicators
        return hasNumbers && hasLetters && (hasDates || hasIdWords || hasFieldIndicators);
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

