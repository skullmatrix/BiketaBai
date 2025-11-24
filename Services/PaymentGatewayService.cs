using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using System.Text.Json;

namespace BiketaBai.Services;

/// <summary>
/// Payment Gateway Service for processing payments via Xendit
/// Supports: GCash, PayMaya, QRPH, Credit/Debit Cards
/// </summary>
public class PaymentGatewayService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://api.xendit.co";
    private readonly string _secretKey;

    public PaymentGatewayService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _secretKey = _configuration["AppSettings:XenditSecretKey"] ?? "";

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Clear();
        // Xendit uses Basic Auth with secret key
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_secretKey + ":"))}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Creates an invoice for GCash, PayMaya, or QRPH
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
                Log.Warning("Xendit secret key not configured");
                return new PaymentIntentResult
                {
                    Success = false,
                    ErrorMessage = "Payment gateway not configured"
                };
            }

            // Map payment method type to Xendit channels
            // Xendit supports multiple payment methods in one invoice
            var enabledChannels = paymentMethodType.ToLower() switch
            {
                "gcash" => new[] { "GCASH" },
                "paymaya" => new[] { "PAYMAYA" },
                "qrph" => new[] { "QRIS" }, // Xendit uses QRIS for QR codes
                _ => new[] { "GCASH", "PAYMAYA", "QRIS" } // Default: enable all
            };

            // Get base URL for redirects
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            var bookingId = metadata?.GetValueOrDefault("booking_id", "");
            
            // Create invoice for e-wallet payments (GCash, PayMaya, QRPH)
            var payload = new
            {
                external_id = $"booking_{bookingId}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                amount = (double)amount,
                description = description,
                invoice_duration = 3600, // 1 hour expiry
                customer = new
                {
                    given_names = metadata?.GetValueOrDefault("customer_name", "Customer"),
                    email = metadata?.GetValueOrDefault("customer_email", "")
                },
                customer_notification_preference = new
                {
                    invoice_created = new[] { "email", "sms" },
                    invoice_reminder = new[] { "email", "sms" },
                    invoice_paid = new[] { "email", "sms" },
                    invoice_expired = new[] { "email", "sms" }
                },
                success_redirect_url = $"{baseUrl}/Bookings/PaymentGateway?bookingId={bookingId}&action=confirm",
                failure_redirect_url = $"{baseUrl}/Bookings/Payment?bookingId={bookingId}",
                currency = currency,
                items = new[]
                {
                    new
                    {
                        name = description,
                        quantity = 1,
                        price = (double)amount
                    }
                },
                // Enable specific payment channels
                enabled_payment_methods = enabledChannels
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Log.Information("Creating Xendit invoice: Amount={Amount}, Channels={Channels}", amount, string.Join(", ", enabledChannels));

            var response = await _httpClient.PostAsync("/v2/invoices", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<XenditInvoiceResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null && !string.IsNullOrEmpty(result.Id))
                {
                    Log.Information("Xendit invoice created: {InvoiceId}", result.Id);
                    return new PaymentIntentResult
                    {
                        Success = true,
                        PaymentIntentId = result.Id,
                        ClientKey = result.InvoiceUrl, // Xendit provides invoice URL
                        NextAction = new { InvoiceUrl = result.InvoiceUrl, Status = result.Status }
                    };
                }
            }

            Log.Error("Failed to create Xendit invoice. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, responseContent);
            
            return new PaymentIntentResult
            {
                Success = false,
                ErrorMessage = $"Failed to create payment invoice: {responseContent}"
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Xendit invoice");
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

            // Xendit uses tokenization for cards
            // First, create a token
            var tokenPayload = new
            {
                card_data = new
                {
                    account_number = cardDetails.CardNumber.Replace(" ", ""),
                    exp_month = cardDetails.ExpMonth.ToString().PadLeft(2, '0'),
                    exp_year = cardDetails.ExpYear.ToString(),
                    cvn = cardDetails.Cvc
                },
                is_single_use = true
            };

            var json = JsonSerializer.Serialize(tokenPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v2/credit_card_tokens", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<XenditTokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null && !string.IsNullOrEmpty(result.Id))
                {
                    return new PaymentMethodResult
                    {
                        Success = true,
                        PaymentMethodId = result.Id
                    };
                }
            }

            Log.Error("Failed to create card token. Status: {StatusCode}, Response: {Response}", 
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
    /// Charges a card using a token
    /// </summary>
    public async Task<PaymentResult> ChargeCardAsync(
        string tokenId,
        decimal amount,
        string currency,
        string description,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            var payload = new
            {
                token_id = tokenId,
                external_id = $"booking_{metadata?.GetValueOrDefault("booking_id", Guid.NewGuid().ToString())}",
                amount = (double)amount,
                currency = currency,
                authentication_id = metadata?.GetValueOrDefault("authentication_id", ""),
                card_cvn = metadata?.GetValueOrDefault("cvv", ""),
                descriptor = description,
                billing_details = new
                {
                    given_names = metadata?.GetValueOrDefault("cardholder_name", ""),
                    email = metadata?.GetValueOrDefault("customer_email", "")
                },
                metadata = metadata
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v2/charges", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<XenditChargeResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null)
                {
                    return new PaymentResult
                    {
                        Success = result.Status == "CAPTURED" || result.Status == "VERIFIED",
                        PaymentIntentId = result.Id,
                        Status = result.Status,
                        TransactionReference = result.Id
                    };
                }
            }

            Log.Error("Failed to charge card. Status: {StatusCode}, Response: {Response}", 
                response.StatusCode, responseContent);

            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Payment failed"
            };
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
    /// Attaches a payment method to a payment intent (for compatibility)
    /// </summary>
    public async Task<PaymentResult> AttachPaymentMethodAsync(
        string paymentIntentId, 
        string paymentMethodId)
    {
        // Xendit doesn't use this pattern - cards are charged directly
        // This is kept for compatibility but should use ChargeCardAsync instead
        return new PaymentResult
        {
            Success = false,
            ErrorMessage = "Use ChargeCardAsync for Xendit card payments"
        };
    }

    /// <summary>
    /// Retrieves invoice/payment status
    /// </summary>
    public async Task<PaymentResult> GetPaymentIntentStatusAsync(string paymentIntentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v2/invoices/{paymentIntentId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<XenditInvoiceResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null)
                {
                    var status = result.Status.ToUpper();
                    var isSuccess = status == "PAID" || status == "SETTLED";

                    return new PaymentResult
                    {
                        Success = isSuccess,
                        PaymentIntentId = paymentIntentId,
                        Status = result.Status,
                        TransactionReference = result.Id
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

    // Xendit API Response Models
    private class XenditInvoiceResponse
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    private class XenditTokenResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private class XenditChargeResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public double Amount { get; set; }
    }
}
