using PaymentGateway.Core.Models;

using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Dtos.Responses;

public class PaymentResponseDto
{
    public Guid Id { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
    public PaymentStatus Status { get; set; }
    public required string CardNumberLastFour { get; set; }
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public int Amount { get; set; }
}
