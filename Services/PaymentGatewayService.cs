using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using System.Text.Json;

namespace BiketaBai.Services;

/// <summary>
/// Payment Gateway Service for processing payments via PayMongo
/// Supports: GCash, PayMaya, GrabPay, Credit/Debit Cards
/// Uses PayMongo Sandbox for testing
/// </summary>
public class PaymentGatewayService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://api.paymongo.com";
    private readonly string _secretKey;

    public PaymentGatewayService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        
        // Get secret key from configuration or environment variables
        _secretKey = _configuration["AppSettings:PayMongoSecretKey"] ?? "";
        
        // Check environment variables (they override appsettings)
        var envSecretKey = Environment.GetEnvironmentVariable("AppSettings__PayMongoSecretKey")
                         ?? Environment.GetEnvironmentVariable("AppSettings:PayMongoSecretKey");
        if (!string.IsNullOrEmpty(envSecretKey))
        {
            _secretKey = envSecretKey;
            Log.Information("PayMongo Secret Key loaded from environment variable");
        }

        // Log configuration status
        if (string.IsNullOrEmpty(_secretKey))
        {
            Log.Warning("PayMongo Secret Key is not configured. Payment gateway will not work.");
        }
        else if (_secretKey.StartsWith("sk_test_", StringComparison.OrdinalIgnoreCase))
        {
            Log.Information("PayMongo configured with Sandbox (Test) keys");
        }
        else if (_secretKey.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("PayMongo configured with Live (Production) keys");
        }
        else
        {
            Log.Warning("PayMongo Secret Key format is invalid. Expected 'sk_test_' or 'sk_live_' prefix");
        }

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Clear();
        
        // PayMongo uses Basic Auth with secret key (sk_test_xxx for sandbox)
        if (!string.IsNullOrEmpty(_secretKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_secretKey + ":"))}");
        }
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Creates a payment intent for GCash, PayMaya, GrabPay, or Card payments
    /// </summary>
    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount, 
        string currency, 
        string paymentMethodType, 
        string description,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_secretKey))
            {
                Log.Warning("PayMongo secret key not configured. Check appsettings.json or environment variables.");
                return new PaymentIntentResult
                {
                    Success = false,
                    ErrorMessage = "Payment gateway not configured. Please add PayMongoSecretKey to appsettings.json or environment variables."
                };
            }

            // Validate secret key format
            if (!_secretKey.StartsWith("sk_", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("PayMongo secret key has invalid format. Expected 'sk_test_' or 'sk_live_' prefix. Current: {KeyPrefix}", 
                    _secretKey.Length > 8 ? _secretKey.Substring(0, 8) : "Invalid");
                return new PaymentIntentResult
                {
                    Success = false,
                    ErrorMessage = "Invalid PayMongo Secret Key format. Expected 'sk_test_' (sandbox) or 'sk_live_' (production) prefix."
                };
            }

            // Get base URL for redirects
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            var bookingId = metadata?.GetValueOrDefault("booking_id", "");
            
            // Convert amount to cents (PayMongo uses smallest currency unit)
            var amountInCents = (long)(amount * 100);
            
            // Create payment intent (PayMongo structure)
            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = amountInCents,
                        currency = currency,
                        payment_method_allowed = paymentMethodType.ToLower() switch
                        {
                            "gcash" => new[] { "gcash" },
                            "paymaya" => new[] { "paymaya" },
                            "grabpay" => new[] { "grab_pay" },
                            "card" => new[] { "card" },
                            _ => new[] { "gcash", "paymaya", "grab_pay", "card" } // Default: enable all
                        },
                        payment_method_options = new
                        {
                            card = new
                            {
                                request_three_d_secure = "automatic"
                            }
                        },
                        description = description,
                        statement_descriptor = "Bike Ta Bai",
                        metadata = metadata ?? new Dictionary<string, string>()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Log.Information("Creating PayMongo payment intent: Amount={Amount}, Method={Method}", amount, paymentMethodType);

            var response = await _httpClient.PostAsync("/v1/payment_intents", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PayMongoPaymentIntentResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data != null && !string.IsNullOrEmpty(result.Data.Id))
                {
                    Log.Information("PayMongo payment intent created: {PaymentIntentId}", result.Data.Id);
                    
                    // Get client key for frontend
                    var clientKey = _configuration["AppSettings:PayMongoPublicKey"] ?? "";
                    
                    return new PaymentIntentResult
                    {
                        Success = true,
                        PaymentIntentId = result.Data.Id,
                        ClientKey = clientKey, // Public key for frontend
                        NextAction = new { 
                            PaymentIntentId = result.Data.Id, 
                            ClientKey = clientKey,
                            Status = result.Data.Attributes.Status 
                        }
                    };
                }
            }

            Log.Error("Failed to create PayMongo payment intent. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, responseContent);
            
            return new PaymentIntentResult
            {
                Success = false,
                ErrorMessage = $"Failed to create payment intent: {responseContent}"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating PayMongo payment intent");
            return new PaymentIntentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a payment method for card payments
    /// </summary>
    public async Task<PaymentMethodResult> CreatePaymentMethodAsync(
        string type,
        CardDetails? cardDetails = null)
    {
        try
        {
            if (type != "card" || cardDetails == null)
            {
                return new PaymentMethodResult
                {
                    Success = false,
                    ErrorMessage = "Card details required for card payments"
                };
            }

            // PayMongo payment method creation
            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        type = "card",
                        details = new
                        {
                            card_number = cardDetails.CardNumber.Replace(" ", ""),
                            exp_month = cardDetails.ExpMonth,
                            exp_year = cardDetails.ExpYear,
                            cvc = cardDetails.Cvc,
                            billing = new
                            {
                                name = cardDetails.CardholderName
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/payment_methods", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PayMongoPaymentMethodResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data != null && !string.IsNullOrEmpty(result.Data.Id))
                {
                    return new PaymentMethodResult
                    {
                        Success = true,
                        PaymentMethodId = result.Data.Id
                    };
                }
            }

            Log.Error("Failed to create payment method. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, responseContent);

            return new PaymentMethodResult
            {
                Success = false,
                ErrorMessage = "Failed to create payment method"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating payment method");
            return new PaymentMethodResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Attaches a payment method to a payment intent (PayMongo pattern)
    /// </summary>
    public async Task<PaymentResult> AttachPaymentMethodAsync(
        string paymentIntentId, 
        string paymentMethodId)
    {
        try
        {
            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        payment_method = paymentMethodId
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/v1/payment_intents/{paymentIntentId}/attach", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PayMongoPaymentIntentResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data != null)
                {
                    var status = result.Data.Attributes.Status;
                    var isSuccess = status == "succeeded" || status == "awaiting_next_action";

                    return new PaymentResult
                    {
                        Success = isSuccess,
                        PaymentIntentId = result.Data.Id,
                        Status = status,
                        TransactionReference = result.Data.Id
                    };
                }
            }

            Log.Error("Failed to attach payment method. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, responseContent);

            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Failed to attach payment method"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error attaching payment method");
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Charges a card using a payment method (legacy method for compatibility)
    /// </summary>
    public async Task<PaymentResult> ChargeCardAsync(
        string paymentMethodId,
        decimal amount,
        string currency,
        string description,
        Dictionary<string, string>? metadata = null)
    {
        // For PayMongo, we need to create a payment intent first, then attach the payment method
        // This is a simplified version - in practice, use CreatePaymentIntentAsync + AttachPaymentMethodAsync
        try
        {
            // Create payment intent
            var intentResult = await CreatePaymentIntentAsync(amount, currency, "card", description, metadata);
            
            if (!intentResult.Success || string.IsNullOrEmpty(intentResult.PaymentIntentId))
            {
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to create payment intent"
                };
            }

            // Attach payment method
            return await AttachPaymentMethodAsync(intentResult.PaymentIntentId, paymentMethodId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error charging card");
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Retrieves payment intent status
    /// </summary>
    public async Task<PaymentResult> GetPaymentIntentStatusAsync(string paymentIntentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v1/payment_intents/{paymentIntentId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PayMongoPaymentIntentResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data != null)
                {
                    var status = result.Data.Attributes.Status;
                    var isSuccess = status == "succeeded";

                    return new PaymentResult
                    {
                        Success = isSuccess,
                        PaymentIntentId = paymentIntentId,
                        Status = status,
                        TransactionReference = result.Data.Id
                    };
                }
            }

            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Failed to retrieve payment status"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving payment status");
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // Result classes
    public class PaymentIntentResult
    {
        public bool Success { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? ClientKey { get; set; }
        public object? NextAction { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? Status { get; set; }
        public string? TransactionReference { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PaymentMethodResult
    {
        public bool Success { get; set; }
        public string? PaymentMethodId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class CardDetails
    {
        public string CardNumber { get; set; } = string.Empty;
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
        public string CardholderName { get; set; } = string.Empty;
    }

    // PayMongo API Response Models
    private class PayMongoPaymentIntentResponse
    {
        public PayMongoPaymentIntentData? Data { get; set; }
    }

    private class PayMongoPaymentIntentData
    {
        public string Id { get; set; } = string.Empty;
        public PayMongoPaymentIntentAttributes Attributes { get; set; } = new();
    }

    private class PayMongoPaymentIntentAttributes
    {
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> PaymentMethodAllowed { get; set; } = new();
    }

    private class PayMongoPaymentMethodResponse
    {
        public PayMongoPaymentMethodData? Data { get; set; }
    }

    private class PayMongoPaymentMethodData
    {
        public string Id { get; set; } = string.Empty;
        public PayMongoPaymentMethodAttributes Attributes { get; set; } = new();
    }

    private class PayMongoPaymentMethodAttributes
    {
        public string Type { get; set; } = string.Empty;
        public PayMongoCardDetails? Details { get; set; }
    }

    private class PayMongoCardDetails
    {
        public string CardNumber { get; set; } = string.Empty;
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
    }
}
