using PaymentGateway.Core.Entities;
using MediatR;

using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.Queries;

public sealed class GetPaymentQuery : IRequest<PaymentEntity?>
{
    public required Guid Id { get; init; }
}
