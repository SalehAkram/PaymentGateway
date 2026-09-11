using PaymentGateway.Core.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Dtos.Requests;
using PaymentGateway.Api.Queries;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    [Fact]
    public async Task ValidRequestReturnsSafePaymentDetailsAndSendsCommand()
    {
        // Arrange
        var request = CreateValidRequest();
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "0007",
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!,
            Amount = request.Amount!.Value
        };
        var sender = new Mock<ISender>();
        sender.Setup(mediator => mediator.Send(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        using var response = await client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert: inspect actual JSON to verify the public contract.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(payment.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Authorized", body.GetProperty("status").GetString());
        Assert.Equal("0007", body.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(payment.ExpiryMonth, body.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(payment.ExpiryYear, body.GetProperty("expiryYear").GetInt32());
        Assert.Equal(payment.Currency, body.GetProperty("currency").GetString());
        Assert.Equal(payment.Amount, body.GetProperty("amount").GetInt32());
        Assert.Equal(7, body.EnumerateObject().Count());
        Assert.False(body.TryGetProperty("cardNumber", out _));
        Assert.False(body.TryGetProperty("cvv", out _));
        sender.Verify(mediator => mediator.Send(It.Is<ProcessPaymentCommand>(command =>
            command.CardNumber == request.CardNumber &&
            command.ExpiryMonth == request.ExpiryMonth &&
            command.ExpiryYear == request.ExpiryYear &&
            command.Currency == request.Currency &&
            command.Amount == request.Amount &&
            command.Cvv == request.Cvv), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidRequestReturnsBadRequestWithoutSendingCommand()
    {
        // Arrange
        var request = CreateValidRequest();
        request.CardNumber = "invalid-card";
        var sender = new Mock<ISender>();
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        using var response = await client.PostAsJsonAsync("/api/v1/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains(nameof(PostPaymentRequestDto.CardNumber), problem!.Errors.Keys);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Payment request rejected.", problem.Title);
        Assert.Equal("Rejected", Assert.IsType<JsonElement>(problem.Extensions["paymentStatus"]).GetString());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        sender.Verify(mediator => mediator.Send(
            It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetReturnsPaymentDetailsForRequestedId()
    {
        // Arrange
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Declined,
            CardNumberLastFour = "0008",
            ExpiryMonth = 4,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050
        };
        var sender = new Mock<ISender>();
        sender.Setup(mediator => mediator.Send(
                It.Is<GetPaymentQuery>(query => query.Id == payment.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        using var response = await client.GetAsync($"/api/v1/payments/{payment.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(payment.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Declined", body.GetProperty("status").GetString());
        Assert.Equal("0008", body.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(payment.ExpiryMonth, body.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(payment.ExpiryYear, body.GetProperty("expiryYear").GetInt32());
        Assert.Equal(payment.Currency, body.GetProperty("currency").GetString());
        Assert.Equal(payment.Amount, body.GetProperty("amount").GetInt32());
        Assert.Equal(7, body.EnumerateObject().Count());
        Assert.False(body.TryGetProperty("cardNumber", out _));
        Assert.False(body.TryGetProperty("cvv", out _));
        sender.Verify(mediator => mediator.Send(
            It.Is<GetPaymentQuery>(query => query.Id == payment.Id), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetReturnsNotFoundWhenPaymentDoesNotExist()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(mediator => mediator.Send(
                It.Is<GetPaymentQuery>(query => query.Id == paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentEntity?)null);
        using var factory = CreateFactory(sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        // Act
        using var response = await client.GetAsync($"/api/v1/payments/{paymentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        sender.Verify(mediator => mediator.Send(
            It.Is<GetPaymentQuery>(query => query.Id == paymentId), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    private static WebApplicationFactory<PaymentsController> CreateFactory(Mock<ISender> sender)
    {
        return new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISender>();
                services.AddSingleton<ISender>(sender.Object);
            }));
    }

    private static PostPaymentRequestDto CreateValidRequest() => new()
    {
        CardNumber = "2222405343240007",
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "012"
    };
}
