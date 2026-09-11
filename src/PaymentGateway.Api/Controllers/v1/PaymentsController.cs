using Microsoft.AspNetCore.Mvc;

using MediatR;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Dtos.Requests;
using PaymentGateway.Api.Dtos.Responses;
using PaymentGateway.Api.Queries;
using PaymentGateway.Core.Entities;

namespace PaymentGateway.Api.Controllers;

[Route("api/v1/payments")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponseDto>> PostPaymentAsync([FromBody] PostPaymentRequestDto request, CancellationToken cancellationToken)
    {
        // ApiController validation ensures required fields are present before this action runs.
        var command = new ProcessPaymentCommand
        {
            CardNumber = request.CardNumber!,
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!,
            Amount = request.Amount!.Value,
            Cvv = request.Cvv!
        };

        var payment = await _sender.Send(command, cancellationToken);

        return Ok(ToResponse(payment));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponseDto>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _sender.Send(new GetPaymentQuery { Id = id }, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(payment));
    }

    private static PaymentResponseDto ToResponse(PaymentEntity payment)
    {
        return new PaymentResponseDto
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };

    }
}
