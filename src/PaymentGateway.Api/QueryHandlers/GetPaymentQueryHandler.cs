using PaymentGateway.Core.Entities;
using MediatR;

using PaymentGateway.Api.Queries;
using PaymentGateway.Core.Abstractions;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.QueryHandlers;

public sealed class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentEntity?>
{
    private readonly IPaymentsRepository _paymentsRepository;

    public GetPaymentQueryHandler(IPaymentsRepository paymentsRepository)
    {
        _paymentsRepository = paymentsRepository;
    }

    public Task<PaymentEntity?> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payment = _paymentsRepository.Get(request.Id);
        return Task.FromResult(payment);
    }
}
