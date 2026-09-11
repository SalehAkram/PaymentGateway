using PaymentGateway.Core.Entities;
using PaymentGateway.Core.Models;
using PaymentGateway.Infra.Persistence;

namespace PaymentGateway.Api.Tests;

public class PaymentsRepositoryTests
{
    [Fact]
    public void AddedPaymentCanBeRetrievedWithItsDetailsPreserved()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var payment = CreatePayment(Guid.NewGuid(), 1050);

        // Act
        repository.Add(payment);
        var retrieved = repository.Get(payment.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(payment.Id, retrieved!.Id);
        Assert.Equal(payment.Status, retrieved.Status);
        Assert.Equal(payment.CardNumberLastFour, retrieved.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, retrieved.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, retrieved.ExpiryYear);
        Assert.Equal(payment.Currency, retrieved.Currency);
        Assert.Equal(payment.Amount, retrieved.Amount);
    }

    [Fact]
    public void DuplicateIdIsRejectedWithoutOverwritingOriginalPayment()
    {
        // Arrange
        var repository = new PaymentsRepository();
        var original = CreatePayment(Guid.NewGuid(), 1050);
        var duplicate = CreatePayment(original.Id, 9999);
        repository.Add(original);

        // Act
        Assert.Throws<InvalidOperationException>(() => repository.Add(duplicate));

        // Assert
        var retrieved = repository.Get(original.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(original.Amount, retrieved!.Amount);
    }

    private static PaymentEntity CreatePayment(Guid id, int amount) => new()
    {
        Id = id,
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = "0007",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = amount
    };
}
