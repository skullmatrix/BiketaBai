using BiketaBai.Helpers;
using BiketaBai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TestOCRModel : PageModel
    {
        private readonly IdValidationService _idValidationService;
        private readonly ILogger<TestOCRModel> _logger;

        public TestOCRModel(IdValidationService idValidationService, ILogger<TestOCRModel> logger)
        {
            _idValidationService = idValidationService;
            _logger = logger;
        }

        [BindProperty]
        public IFormFile? IdDocument { get; set; }

        public OCRTestResult? Result { get; set; }

        public class OCRTestResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? FullText { get; set; }
            public int TextLength { get; set; }
            public int VerificationScore { get; set; }
            public string? ExtractedName { get; set; }
            public string? ExtractedAddress { get; set; }
            public Dictionary<string, string> ExtractedFields { get; set; } = new();
            public bool IsValid { get; set; }
            public string? ValidationMessage { get; set; }
            public List<string> MatchedPatterns { get; set; } = new();
            public int MeaningfulLines { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!AuthHelper.IsAdmin(User))
                return RedirectToPage("/Account/AccessDenied");

            if (IdDocument == null || IdDocument.Length == 0)
            {
                Result = new OCRTestResult
                {
                    Success = false,
                    ErrorMessage = "Please upload an ID document image"
                };
                return Page();
            }

            try
            {
                // Validate the ID
                var validationResult = await _idValidationService.ValidateIdAsync(IdDocument);

                // Get the full OCR text by calling the Vision API directly (for debugging)
                var fullText = await GetFullOCRTextAsync(IdDocument);
                
                // Calculate verification score manually for display
                var score = CalculateVerificationScore(fullText, validationResult.ExtractedFields);
                var meaningfulLines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Count(line => line.Trim().Length > 5);
                
                // Get matched patterns
                var matchedPatterns = GetMatchedPatterns(fullText);

                Result = new OCRTestResult
                {
                    Success = true,
                    FullText = fullText,
                    TextLength = fullText?.Length ?? 0,
                    VerificationScore = score,
                    ExtractedName = validationResult.ExtractedName,
                    ExtractedAddress = validationResult.ExtractedAddress,
                    ExtractedFields = validationResult.ExtractedFields,
                    IsValid = validationResult.IsValid,
                    ValidationMessage = validationResult.IsValid ? "✅ ID is Valid" : validationResult.ErrorMessage ?? "❌ ID is Invalid",
                    MatchedPatterns = matchedPatterns,
                    MeaningfulLines = meaningfulLines
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error testing OCR");
                Result = new OCRTestResult
                {
                    Success = false,
                    ErrorMessage = $"Error: {ex.Message}"
                };
            }

            return Page();
        }

        private async Task<string> GetFullOCRTextAsync(IFormFile idDocument)
        {
            var configuration = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var apiKey = configuration["AppSettings:GoogleCloudVisionApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return "❌ Google Cloud Vision API key not configured";
            }

            try
            {
                // Convert image to base64
                using var memoryStream = new MemoryStream();
                await idDocument.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var base64Image = Convert.ToBase64String(imageBytes);

                // Call Google Cloud Vision API
                using var httpClient = new System.Net.Http.HttpClient();
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

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(
                    $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var visionResponse = System.Text.Json.JsonSerializer.Deserialize<VisionApiResponse>(responseContent);

                    if (visionResponse?.Responses != null && visionResponse.Responses.Length > 0 &&
                        visionResponse.Responses[0] != null)
                    {
                        var textAnnotations = visionResponse.Responses[0].TextAnnotations;
                        if (textAnnotations != null && textAnnotations.Length > 0)
                        {
                            return textAnnotations[0].Description ?? "No text extracted";
                        }
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return $"❌ API Error ({response.StatusCode}): {errorContent}";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error: {ex.Message}";
            }

            return "No text extracted from image";
        }

        private int CalculateVerificationScore(string fullText, Dictionary<string, string> extractedFields)
        {
            if (string.IsNullOrWhiteSpace(fullText))
                return 0;

            int score = 0;
            var upperText = fullText.ToUpperInvariant();

            // Check 1: ID number patterns
            var idNumberPatterns = new[] { 
                "ID NO", "ID NUMBER", "ID#", "ID:", "IDENTIFICATION", 
                "LICENSE NO", "LICENSE NUMBER", "LICENSE#", 
                "PASSPORT NO", "PASSPORT NUMBER", "PASSPORT#",
                "SSS NO", "SSS NUMBER", "SSS#",
                "TIN NO", "TIN NUMBER", "TIN#",
                "DRIVER", "DRIVER'S", "DRIVERS"
            };
            if (idNumberPatterns.Any(pattern => upperText.Contains(pattern)))
                score++;

            // Check 2: Date of birth
            var dobPatterns = new[] { 
                "DATE OF BIRTH", "DOB", "BIRTH DATE", "BIRTHDATE", 
                "BORN", "BIRTH", "MM/DD/YYYY", "DD/MM/YYYY", 
                "YYYY-MM-DD"
            };
            if (dobPatterns.Any(pattern => upperText.Contains(pattern)) || 
                extractedFields.ContainsKey("DateOfBirth"))
                score++;

            // Check 3: Address
            var addressPatterns = new[] { 
                "ADDRESS", "RESIDENCE", "HOME ADDRESS", "PERMANENT ADDRESS",
                "STREET", "BARANGAY", "CITY", "PROVINCE"
            };
            if (addressPatterns.Any(pattern => upperText.Contains(pattern)))
                score++;

            // Check 4: Expiration date
            var expiryPatterns = new[] { 
                "EXPIR", "EXPIRES", "VALID UNTIL", "VALID THRU", 
                "VALID UNTIL", "EFFECTIVE", "ISSUED", "VALIDITY",
                "MM/DD/YYYY", "DD/MM/YYYY"
            };
            if (expiryPatterns.Any(pattern => upperText.Contains(pattern)))
                score++;

            // Check 5: Name field
            var namePatterns = new[] { 
                "NAME", "FULL NAME", "LAST NAME", "FIRST NAME", 
                "GIVEN NAME", "SURNAME", "FAMILY NAME"
            };
            if (namePatterns.Any(pattern => upperText.Contains(pattern)))
                score++;

            // Bonus: Government agency keywords
            var agencyPatterns = new[] { 
                "PHILIPPINES", "REPUBLIC", "LTO", "NBI", "SSS", 
                "PHILHEALTH", "PAG-IBIG", "TIN", "CIVIL", "COMMISSION",
                "DEPARTMENT", "NATIONAL", "PHILIPPINE"
            };
            if (agencyPatterns.Any(pattern => upperText.Contains(pattern)))
                score++;

            return Math.Min(5, Math.Max(0, score));
        }

        private List<string> GetMatchedPatterns(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText))
                return new List<string>();

            var patterns = new List<string>();
            var upperText = fullText.ToUpperInvariant();

            // ID number patterns
            var idNumberPatterns = new[] { "ID NO", "ID NUMBER", "ID#", "LICENSE NO", "PASSPORT NO" };
            foreach (var pattern in idNumberPatterns)
            {
                if (upperText.Contains(pattern))
                    patterns.Add($"✓ ID Number pattern: {pattern}");
            }

            // DOB patterns
            var dobPatterns = new[] { "DATE OF BIRTH", "DOB", "BIRTH DATE", "BORN" };
            foreach (var pattern in dobPatterns)
            {
                if (upperText.Contains(pattern))
                    patterns.Add($"✓ Date of Birth pattern: {pattern}");
            }

            // Address patterns
            var addressPatterns = new[] { "ADDRESS", "STREET", "BARANGAY", "CITY", "PROVINCE" };
            foreach (var pattern in addressPatterns)
            {
                if (upperText.Contains(pattern))
                    patterns.Add($"✓ Address pattern: {pattern}");
            }

            // Name patterns
            var namePatterns = new[] { "NAME", "FULL NAME", "LAST NAME", "FIRST NAME" };
            foreach (var pattern in namePatterns)
            {
                if (upperText.Contains(pattern))
                    patterns.Add($"✓ Name pattern: {pattern}");
            }

            // Government agency patterns
            var agencyPatterns = new[] { "PHILIPPINES", "REPUBLIC", "LTO", "NBI", "SSS" };
            foreach (var pattern in agencyPatterns)
            {
                if (upperText.Contains(pattern))
                    patterns.Add($"✓ Government agency: {pattern}");
            }

            return patterns;
        }

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
}

