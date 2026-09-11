using PaymentGateway.Core.Entities;
using System.Collections.Concurrent;

using PaymentGateway.Core.Abstractions;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Infra.Persistence;

// In-memory storage for the exercise. Payments are lost when the application restarts.
public sealed class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentEntity> _payments = new();

    public PaymentEntity? Get(Guid id)
    {
        return _payments.TryGetValue(id, out var payment) ? payment : null;
    }

    public void Add(PaymentEntity payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException($"Payment {payment.Id} already exists.");
        }
    }
}
