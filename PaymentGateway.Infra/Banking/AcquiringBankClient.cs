using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Core.Abstractions;
using PaymentGateway.Core.Exceptions;
using PaymentGateway.Core.Models;
using PaymentGateway.Infra.Banking.Models;

namespace PaymentGateway.Infra.Banking;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankPaymentResult> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        var bankRequest = new BankRequestDto
        {
            CardNumber = request.CardNumber,
            ExpiryDate = string.Format(CultureInfo.InvariantCulture, "{0:D2}/{1:D4}", request.ExpiryMonth, request.ExpiryYear),
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };

        _logger.LogInformation(
            "Bank payment request started: CardLastFour={CardLastFour}, Currency={Currency}, Amount={Amount} minor units.",
            request.CardNumber[^4..], request.Currency, request.Amount);

        var stopwatch = Stopwatch.StartNew();
        int? httpStatusCode = null;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("payments", bankRequest, cancellationToken);
            httpStatusCode = (int)response.StatusCode;

            // HTTP failures are not bank declines; allow them to propagate to the caller.
            response.EnsureSuccessStatusCode();

            var bankResponse = await response.Content.ReadFromJsonAsync<BankResponseDto>(
                cancellationToken: cancellationToken)
                ?? throw new JsonException("The bank returned a null payment response.");

            stopwatch.Stop();
            _logger.LogInformation(
                "Bank payment completed: HttpStatusCode={HttpStatusCode}, Authorized={Authorized}, ElapsedMilliseconds={ElapsedMilliseconds}.",
                httpStatusCode, bankResponse.Authorized, stopwatch.Elapsed.TotalMilliseconds);

            return new BankPaymentResult
            {
                Authorized = bankResponse.Authorized
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            stopwatch.Stop();
            var failureCategory = exception switch
            {
                OperationCanceledException when cancellationToken.IsCancellationRequested => "Cancelled",
                OperationCanceledException => "Timeout",
                JsonException => "InvalidResponse",
                _ => "HttpFailure"
            };

            // Timing metadata stays here; the API owns the error-level failure log.
            _logger.LogInformation(
                "Bank payment failed: FailureCategory={FailureCategory}, HttpStatusCode={HttpStatusCode}, ElapsedMilliseconds={ElapsedMilliseconds}.",
                failureCategory, httpStatusCode, stopwatch.Elapsed.TotalMilliseconds);
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            var failure = exception switch
            {
                OperationCanceledException => BankFailure.Timeout,
                JsonException => BankFailure.InvalidResponse,
                HttpRequestException when httpStatusCode is null or 503 => BankFailure.Unavailable,
                _ => BankFailure.InvalidResponse
            };

            // Do not carry raw upstream exception messages or response content into API errors.
            throw new BankException(failure);
        }
    }
}
