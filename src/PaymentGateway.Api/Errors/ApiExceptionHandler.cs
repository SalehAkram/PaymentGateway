using System.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Core.Exceptions;

namespace PaymentGateway.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The caller has disconnected; do not report a bank timeout or write a response body.
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (statusCode, title) = exception switch
        {
            BankException { Failure: BankFailure.Unavailable } => (503, "The acquiring bank is unavailable."),
            BankException { Failure: BankFailure.Timeout } => (504, "The acquiring bank did not respond in time."),
            BankException { Failure: BankFailure.InvalidResponse } => (502, "The acquiring bank returned an invalid response."),
            _ => (500, "An unexpected error occurred.")
        };

        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        _logger.LogError(
            "Request failed: ExceptionType={ExceptionType}, HttpStatusCode={HttpStatusCode}, TraceId={TraceId}.",
            exception.GetType().Name, statusCode, traceId);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };
        problem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
