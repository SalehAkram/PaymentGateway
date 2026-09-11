using PaymentGateway.Core.Entities;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using PaymentGateway.Api.CommandHandlers;
using PaymentGateway.Api.Commands;
using PaymentGateway.Core.Abstractions;
using PaymentGateway.Core.Models;

namespace PaymentGateway.Api.Tests;

public class ProcessPaymentCommandHandlerTests
{
    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task BankDecisionIsSavedAndReturned(bool authorized, PaymentStatus expectedStatus)
    {
        // Arrange
        var bank = new Mock<IAcquiringBankClient>();
        bank.Setup(client => client.ProcessPaymentAsync( It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResult { Authorized = authorized });
        var repository = new Mock<IPaymentsRepository>();
        var handler = new ProcessPaymentCommandHandler(bank.Object, repository.Object, NullLogger<ProcessPaymentCommandHandler>.Instance);
        var command = CreateCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(expectedStatus, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("0007", result.CardNumberLastFour);
        Assert.Equal(command.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(command.ExpiryYear, result.ExpiryYear);
        Assert.Equal(command.Currency, result.Currency);
        Assert.Equal(command.Amount, result.Amount);
        repository.Verify(storage => storage.Add(result), Times.Once);
        repository.VerifyNoOtherCalls();
        bank.Verify(client => client.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullPaymentDetailsAndCancellationTokenArePassedToBank()
    {
        // Arrange
        var bank = new Mock<IAcquiringBankClient>();
        bank.Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResult { Authorized = true });
        var repository = new Mock<IPaymentsRepository>();
        var handler = new ProcessPaymentCommandHandler(bank.Object, repository.Object, NullLogger<ProcessPaymentCommandHandler>.Instance);
        var command = CreateCommand();
        using var cancellation = new CancellationTokenSource();

        // Act
        await handler.Handle(command, cancellation.Token);

        // Assert
        bank.Verify(client => client.ProcessPaymentAsync(
            It.Is<BankPaymentRequest>(request =>
                request.CardNumber == command.CardNumber &&
                request.Cvv == command.Cvv &&
                request.ExpiryMonth == command.ExpiryMonth &&
                request.ExpiryYear == command.ExpiryYear &&
                request.Currency == command.Currency &&
                request.Amount == command.Amount),
            cancellation.Token), Times.Once);
        bank.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BankFailurePropagatesWithoutSavingPayment()
    {
        // Arrange
        var failure = new HttpRequestException("Bank unavailable.");
        var bank = new Mock<IAcquiringBankClient>();
        bank.Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var repository = new Mock<IPaymentsRepository>();
        var handler = new ProcessPaymentCommandHandler(bank.Object, repository.Object, NullLogger<ProcessPaymentCommandHandler>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => handler.Handle(CreateCommand(), CancellationToken.None));

        // Assert
        Assert.Same(failure, exception);
        repository.Verify(storage => storage.Add(It.IsAny<PaymentEntity>()), Times.Never);
        bank.Verify(client => client.ProcessPaymentAsync(
            It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PersistenceFailureAfterAuthorizationPropagatesWithoutRetryingBank()
    {
        // Arrange
        var failure = new InvalidOperationException("Storage unavailable.");
        var bank = new Mock<IAcquiringBankClient>();
        bank.Setup(client => client.ProcessPaymentAsync(
                It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankPaymentResult { Authorized = true });
        var repository = new Mock<IPaymentsRepository>();
        repository.Setup(storage => storage.Add(It.IsAny<PaymentEntity>())).Throws(failure);
        var handler = new ProcessPaymentCommandHandler(bank.Object, repository.Object, NullLogger<ProcessPaymentCommandHandler>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Same(failure, exception);
        bank.Verify(client => client.ProcessPaymentAsync(
            It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(storage => storage.Add(It.IsAny<PaymentEntity>()), Times.Once);
    }

    private static ProcessPaymentCommand CreateCommand() => new()
    {
        CardNumber = "2222405343240007",
        ExpiryMonth = 12,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "012"
    };
}
