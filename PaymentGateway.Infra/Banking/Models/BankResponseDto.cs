using System.Text.Json.Serialization;

namespace PaymentGateway.Infra.Banking.Models;

internal sealed class BankResponseDto
{
    // A missing decision must fail deserialization rather than default to a decline.
    [JsonPropertyName("authorized")]
    public required bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public required string AuthorizationCode { get; init; }
}
