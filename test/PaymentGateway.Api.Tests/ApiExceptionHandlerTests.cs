using System.Net.Http.Json;
using System.Text.Json;

using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Core.Exceptions;

namespace PaymentGateway.Api.Tests;

public class ApiExceptionHandlerTests
{
    [Theory]
    [InlineData(BankFailure.Unavailable, 503)]
    [InlineData(BankFailure.Timeout, 504)]
    [InlineData(BankFailure.InvalidResponse, 502)]
    [InlineData(null, 500)]
    public async Task FailureReturnsSafeProblemDetails(BankFailure? failure, int expectedStatus)
    {
        Exception exception = failure.HasValue
            ? new BankException(failure.Value)
            : new InvalidOperationException("Private internal failure details");
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        using var factory = new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Keep test logging independent of Windows Event Log permissions.
                services.AddLogging(logging => logging.ClearProviders().AddConsole());
                services.RemoveAll<ISender>();
                services.AddSingleton<ISender>(sender.Object);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync("/api/v1/payments", new
        {
            CardNumber = "2222405343240007",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 1050,
            Cvv = "012"
        });

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("Private internal failure details", content);
        Assert.DoesNotContain("2222405343240007", content);
        Assert.False(json.RootElement.TryGetProperty("exception", out _));
        Assert.False(json.RootElement.TryGetProperty("stackTrace", out _));
    }
}
