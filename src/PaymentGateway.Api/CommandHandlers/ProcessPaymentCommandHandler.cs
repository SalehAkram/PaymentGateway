using PaymentGateway.Core.Entities;
using System.Diagnostics;

using MediatR;

using PaymentGateway.Api.Commands;
using PaymentGateway.Core.Abstractions;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.CommandHandlers;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, PaymentEntity>
{
    private readonly IAcquiringBankClient _bankClient;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(IAcquiringBankClient bankClient,IPaymentsRepository paymentsRepository,ILogger<ProcessPaymentCommandHandler> logger)
    {
        _bankClient = bankClient;
        _paymentsRepository = paymentsRepository;
        _logger = logger;
    }

    // Consistency limitation: the bank may process the payment but saving it locally may fail.
    // For production, consider persisting a Processing record first with a stable reference (Idempotency key, i.e. paymentID),
    // recovery needs bank-supported status lookup/idempotency and background reconciliation.
    // Merchants could track pending payments through polling or webhooks.
    // The supplied simulator lacks those recovery capabilities, so this flow stays synchronous.
    public async Task<PaymentEntity> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Payment processing started.");

        var bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        var bankResult = await _bankClient.ProcessPaymentAsync(bankRequest, cancellationToken);

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            Status = bankResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        _paymentsRepository.Add(payment);

        stopwatch.Stop();
        _logger.LogInformation("Payment {PaymentId} processed with status {Status} in {ElapsedMilliseconds} ms.", payment.Id, payment.Status, stopwatch.Elapsed.TotalMilliseconds);

        return payment;
    }
}
