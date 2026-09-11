using PaymentGateway.Core.Entities;
using MediatR;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.Commands;

public sealed class ProcessPaymentCommand : IRequest<PaymentEntity>
{
    public required string CardNumber { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public required int Amount { get; init; }
    public required string Cvv { get; init; }
}
