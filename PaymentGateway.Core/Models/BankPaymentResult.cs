namespace PaymentGateway.Core.Models;

public sealed class BankPaymentResult
{
    public required bool Authorized { get; init; }
}
