using PaymentGateway.Core.Models;

namespace PaymentGateway.Core.Abstractions;

public interface IAcquiringBankClient
{
    Task<BankPaymentResult> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}
