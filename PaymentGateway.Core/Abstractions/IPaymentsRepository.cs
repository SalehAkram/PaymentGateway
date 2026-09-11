using PaymentGateway.Core.Entities;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Core.Abstractions;

public interface IPaymentsRepository
{
    void Add(PaymentEntity payment);
    PaymentEntity? Get(Guid id);
}
