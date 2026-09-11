using System.ComponentModel.DataAnnotations;

using PaymentGateway.Api.Dtos.Requests;

namespace PaymentGateway.Api.Tests;

public class PostPaymentRequestDtoTests
{
    [Theory]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), null)]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), "")]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), "1234567890123")]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), "12345678901234567890")]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), "1234567890123a")]
    [InlineData(nameof(PostPaymentRequestDto.CardNumber), "１２３４５６７８９０１２３４")]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryMonth), null)]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryMonth), 0)]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryMonth), 13)]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryYear), null)]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryYear), 0)]
    [InlineData(nameof(PostPaymentRequestDto.ExpiryYear), 10000)]
    [InlineData(nameof(PostPaymentRequestDto.Currency), null)]
    [InlineData(nameof(PostPaymentRequestDto.Currency), "JPY")]
    [InlineData(nameof(PostPaymentRequestDto.Currency), "gbp")]
    [InlineData(nameof(PostPaymentRequestDto.Currency), "GB")]
    [InlineData(nameof(PostPaymentRequestDto.Currency), "GBPX")]
    [InlineData(nameof(PostPaymentRequestDto.Amount), null)]
    [InlineData(nameof(PostPaymentRequestDto.Cvv), null)]
    [InlineData(nameof(PostPaymentRequestDto.Cvv), "12")]
    [InlineData(nameof(PostPaymentRequestDto.Cvv), "12345")]
    [InlineData(nameof(PostPaymentRequestDto.Cvv), "12a")]
    [InlineData(nameof(PostPaymentRequestDto.Cvv), "   ")]
    public void InvalidFieldIsRejected(string propertyName, object? value)
    {
        var request = ValidRequest();
        typeof(PostPaymentRequestDto).GetProperty(propertyName)!.SetValue(request, value);

        Assert.Contains(Validate(request), error => error.MemberNames.Contains(propertyName));
    }

    [Theory]
    [InlineData("00000000000001", "012", "GBP")]
    [InlineData("1234567890123456789", "0123", "USD")]
    [InlineData("2222405343248877", "123", "EUR")]
    public void ValidFormatsAreAccepted(string cardNumber, string cvv, string currency)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;
        request.Cvv = cvv;
        request.Currency = currency;

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(12, 2025, false)]
    [InlineData(8, 2026, false)]
    [InlineData(9, 2026, true)]
    [InlineData(10, 2026, true)]
    [InlineData(1, 2027, true)]
    public void ExpiryIsCheckedAsCombinedMonthAndYear(int month, int year, bool valid)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Equal(valid, Validate(request).Count == 0);
    }

    [Fact]
    public void DecemberExpiresWhenJanuaryBegins()
    {
        var request = ValidRequest();
        request.ExpiryMonth = 12;
        request.ExpiryYear = 2026;

        Assert.Empty(Validate(request, new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero)));
        Assert.NotEmpty(Validate(request, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void AmountHasNoAdditionalRangeRestriction(int amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Assert.Empty(Validate(request));
    }

    private static PostPaymentRequestDto ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 10,
        ExpiryYear = 2026,
        Currency = "GBP",
        Amount = 100,
        Cvv = "012"
    };

    private static List<ValidationResult> Validate(PostPaymentRequestDto request, DateTimeOffset? now = null)
    {
        var clock = new FixedTimeProvider(now ?? new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
        var context = new ValidationContext(request);
        context.InitializeServiceProvider(type => type == typeof(TimeProvider) ? clock : null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
