using BiketaBai.Helpers;
using BiketaBai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TestPayMongoModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly PaymentGatewayService _paymentGatewayService;

        public TestPayMongoModel(IConfiguration configuration, PaymentGatewayService paymentGatewayService)
        {
            _configuration = configuration;
            _paymentGatewayService = paymentGatewayService;
        }

        [BindProperty]
        public decimal TestAmount { get; set; } = 100.00m;

        [BindProperty]
        public string TestPaymentMethod { get; set; } = "gcash";

        public TestResult? Result { get; set; }
        public PayMongoConfigInfo ConfigInfo { get; set; } = new();

        public class PayMongoConfigInfo
        {
            public string SecretKeyMasked { get; set; } = string.Empty;
            public string PublicKeyMasked { get; set; } = string.Empty;
            public string WebhookSecretMasked { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public bool IsConfigured { get; set; }
            public string ConfigSource { get; set; } = string.Empty;
            public string KeyType { get; set; } = string.Empty; // "test" or "live"
        }

        public class TestResult
        {
            public bool Success { get; set; }
            public List<TestStep> Steps { get; set; } = new();
            public string? ErrorMessage { get; set; }
            public string? FullException { get; set; }
            public string? PaymentIntentId { get; set; }
            public string? ResponseDetails { get; set; }
        }

        public class TestStep
        {
            public string Name { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? Details { get; set; }
        }

        public void OnGet()
        {
            LoadConfigInfo();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!AuthHelper.IsAdmin(User))
                return RedirectToPage("/Account/AccessDenied");

            LoadConfigInfo();

            if (TestAmount <= 0)
            {
                Result = new TestResult
                {
                    Success = false,
                    ErrorMessage = "Test amount must be greater than 0"
                };
                return Page();
            }

            Result = await RunPayMongoTestAsync(TestAmount, TestPaymentMethod);
            return Page();
        }

        private void LoadConfigInfo()
        {
            // Get values from configuration
            var secretKey = _configuration["AppSettings:PayMongoSecretKey"] ?? "";
            var publicKey = _configuration["AppSettings:PayMongoPublicKey"] ?? "";
            var webhookSecret = _configuration["AppSettings:PayMongoWebhookSecret"] ?? "";
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";

            // Check environment variables (they override appsettings)
            // Support multiple formats: AppSettings__Key, AppSettings:Key, Key
            var envSecretKey = Environment.GetEnvironmentVariable("AppSettings__PayMongoSecretKey")
                             ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoSecretKey")
                             ?? Environment.GetEnvironmentVariable("PayMongoSecretKey");
            var envPublicKey = Environment.GetEnvironmentVariable("AppSettings__PayMongoPublicKey")
                            ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoPublicKey")
                            ?? Environment.GetEnvironmentVariable("PayMongoPublicKey");
            var envWebhookSecret = Environment.GetEnvironmentVariable("AppSettings__PayMongoWebhookSecret")
                                 ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoWebhookSecret")
                                 ?? Environment.GetEnvironmentVariable("PayMongoWebhookSecret");
            var envBaseUrl = Environment.GetEnvironmentVariable("AppSettings__BaseUrl")
                          ?? Environment.GetEnvironmentVariable("AppSettings:BaseUrl")
                          ?? Environment.GetEnvironmentVariable("BaseUrl");

            // Use environment variables if they exist
            if (!string.IsNullOrEmpty(envSecretKey)) secretKey = envSecretKey;
            if (!string.IsNullOrEmpty(envPublicKey)) publicKey = envPublicKey;
            if (!string.IsNullOrEmpty(envWebhookSecret)) webhookSecret = envWebhookSecret;
            if (!string.IsNullOrEmpty(envBaseUrl)) baseUrl = envBaseUrl;

            // Mask keys for display
            ConfigInfo.SecretKeyMasked = MaskKey(secretKey);
            ConfigInfo.PublicKeyMasked = MaskKey(publicKey);
            ConfigInfo.WebhookSecretMasked = MaskKey(webhookSecret);
            ConfigInfo.BaseUrl = baseUrl;

            // Determine key type
            if (secretKey.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
            {
                ConfigInfo.KeyType = "Test (Sandbox)";
            }
            else if (secretKey.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase))
            {
                ConfigInfo.KeyType = "Live (Production)";
            }
            else if (string.IsNullOrEmpty(secretKey))
            {
                ConfigInfo.KeyType = "Not Set";
            }
            else
            {
                ConfigInfo.KeyType = "Unknown Format";
            }

            // Check if configured
            ConfigInfo.IsConfigured = !string.IsNullOrEmpty(secretKey) &&
                                     !string.IsNullOrEmpty(publicKey) &&
                                     secretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase) &&
                                     publicKey.StartsWith("pk_", StringComparison.OrdinalIgnoreCase);

            // Determine config source
            var hasEnvVars = !string.IsNullOrEmpty(envSecretKey) ||
                           !string.IsNullOrEmpty(envPublicKey) ||
                           !string.IsNullOrEmpty(envWebhookSecret) ||
                           !string.IsNullOrEmpty(envBaseUrl);

            if (hasEnvVars)
            {
                var envVars = new List<string>();
                if (!string.IsNullOrEmpty(envSecretKey)) envVars.Add("PayMongoSecretKey");
                if (!string.IsNullOrEmpty(envPublicKey)) envVars.Add("PayMongoPublicKey");
                if (!string.IsNullOrEmpty(envWebhookSecret)) envVars.Add("PayMongoWebhookSecret");
                if (!string.IsNullOrEmpty(envBaseUrl)) envVars.Add("BaseUrl");

                ConfigInfo.ConfigSource = $"✅ Environment Variables\n   ({string.Join(", ", envVars)})";
            }
            else
            {
                var configFile = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development"
                    ? "appsettings.Development.json"
                    : "appsettings.json";
                ConfigInfo.ConfigSource = $"📄 Configuration File: {configFile}";
            }
        }

        private string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "Not set";

            if (key.Length <= 8)
                return "***";

            // Show first 4 and last 4 characters
            return key.Substring(0, 4) + "***" + key.Substring(key.Length - 4);
        }

        private async Task<TestResult> RunPayMongoTestAsync(decimal amount, string paymentMethod)
        {
            var result = new TestResult { Steps = new List<TestStep>() };

            try
            {
                // Step 1: Check Configuration
                var step1 = new TestStep { Name = "1. Configuration Check" };
                var secretKey = _configuration["AppSettings:PayMongoSecretKey"] ?? "";
                var publicKey = _configuration["AppSettings:PayMongoPublicKey"] ?? "";

                // Check environment variables (support multiple formats)
                var envSecretKey = Environment.GetEnvironmentVariable("AppSettings__PayMongoSecretKey")
                                 ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoSecretKey")
                                 ?? Environment.GetEnvironmentVariable("PayMongoSecretKey");
                var envPublicKey = Environment.GetEnvironmentVariable("AppSettings__PayMongoPublicKey")
                                ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoPublicKey")
                                ?? Environment.GetEnvironmentVariable("PayMongoPublicKey");

                if (!string.IsNullOrEmpty(envSecretKey)) secretKey = envSecretKey;
                if (!string.IsNullOrEmpty(envPublicKey)) publicKey = envPublicKey;

                if (string.IsNullOrEmpty(secretKey))
                {
                    step1.Success = false;
                    step1.Message = "❌ PayMongo Secret Key is not configured";
                    step1.Details = "Please add your PayMongo Secret Key (sk_test_xxx for sandbox) to appsettings.json or environment variables";
                    result.Steps.Add(step1);
                    result.ErrorMessage = "PayMongo Secret Key is missing";
                    return result;
                }

                if (!secretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
                {
                    step1.Success = false;
                    step1.Message = "❌ Invalid Secret Key format";
                    step1.Details = $"Secret key should start with 'sk_test_' (sandbox) or 'sk_live_' (production). Current: {MaskKey(secretKey)}";
                    result.Steps.Add(step1);
                    result.ErrorMessage = "Invalid Secret Key format";
                    return result;
                }

                if (string.IsNullOrEmpty(publicKey))
                {
                    step1.Success = false;
                    step1.Message = "⚠️ PayMongo Public Key is not configured (optional for testing)";
                    step1.Details = "Public key is needed for frontend integration. Add pk_test_xxx for sandbox.";
                }
                else if (!publicKey.StartsWith("pk_", StringComparison.OrdinalIgnoreCase))
                {
                    step1.Success = false;
                    step1.Message = "❌ Invalid Public Key format";
                    step1.Details = $"Public key should start with 'pk_test_' (sandbox) or 'pk_live_' (production). Current: {MaskKey(publicKey)}";
                    result.Steps.Add(step1);
                    result.ErrorMessage = "Invalid Public Key format";
                    return result;
                }
                else
                {
                    step1.Success = true;
                    step1.Message = "✅ Configuration loaded successfully";
                    step1.Details = $"Secret Key: {MaskKey(secretKey)}, Public Key: {MaskKey(publicKey)}, Key Type: {(secretKey.StartsWith("sk_test_") ? "Sandbox (Test)" : "Live (Production)")}";
                }

                result.Steps.Add(step1);

                // Step 2: Test API Connectivity
                var step2 = new TestStep { Name = "2. API Connectivity Test" };
                Log.Information("Testing PayMongo API connectivity");

                // Create a test payment intent with minimal amount
                var testResult = await _paymentGatewayService.CreatePaymentIntentAsync(
                    amount,
                    "PHP",
                    paymentMethod,
                    $"Test payment - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                    new Dictionary<string, string>
                    {
                        { "test", "true" },
                        { "test_timestamp", DateTime.UtcNow.ToString("O") }
                    }
                );

                if (!testResult.Success)
                {
                    step2.Success = false;
                    step2.Message = "❌ Failed to create payment intent";
                    step2.Details = testResult.ErrorMessage ?? "Unknown error";
                    result.Steps.Add(step2);
                    result.ErrorMessage = testResult.ErrorMessage ?? "Failed to connect to PayMongo API";
                    result.ResponseDetails = testResult.ErrorMessage;
                    return result;
                }

                if (string.IsNullOrEmpty(testResult.PaymentIntentId))
                {
                    step2.Success = false;
                    step2.Message = "❌ Payment intent created but no ID returned";
                    step2.Details = "PayMongo API responded but did not return a payment intent ID";
                    result.Steps.Add(step2);
                    result.ErrorMessage = "Invalid response from PayMongo API";
                    return result;
                }

                step2.Success = true;
                step2.Message = "✅ Successfully connected to PayMongo API";
                step2.Details = $"Payment Intent ID: {testResult.PaymentIntentId}";
                result.Steps.Add(step2);
                result.PaymentIntentId = testResult.PaymentIntentId;

                // Step 3: Verify Payment Intent Status
                var step3 = new TestStep { Name = "3. Payment Intent Verification" };
                var statusResult = await _paymentGatewayService.GetPaymentIntentStatusAsync(testResult.PaymentIntentId);

                if (!statusResult.Success)
                {
                    step3.Success = false;
                    step3.Message = "⚠️ Could not verify payment intent status";
                    step3.Details = statusResult.ErrorMessage ?? "Status check failed";
                }
                else
                {
                    step3.Success = true;
                    step3.Message = "✅ Payment intent verified";
                    step3.Details = $"Status: {statusResult.Status ?? "Unknown"}";
                }

                result.Steps.Add(step3);

                // Step 4: Configuration Summary
                var step4 = new TestStep { Name = "4. Configuration Summary" };
                step4.Success = true;
                step4.Message = "✅ All tests passed";
                step4.Details = $"Payment gateway is properly configured. Test payment intent created successfully with ID: {testResult.PaymentIntentId}";
                result.Steps.Add(step4);

                result.Success = true;
                result.ResponseDetails = $"Payment Intent ID: {testResult.PaymentIntentId}\nStatus: {statusResult.Status ?? "Created"}";

                Log.Information("PayMongo test completed successfully. Payment Intent ID: {PaymentIntentId}", testResult.PaymentIntentId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during PayMongo test");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.FullException = ex.ToString();

                var errorStep = new TestStep
                {
                    Name = "Error",
                    Success = false,
                    Message = "❌ Exception occurred during test",
                    Details = ex.Message
                };
                result.Steps.Add(errorStep);
            }

            return result;
        }
    }
}

