namespace PaymentGateway.Api.Dtos.Requests;

using System.ComponentModel.DataAnnotations;

public class PostPaymentRequestDto : IValidatableObject
{
    [Required]
    [RegularExpression("[0-9]{14,19}", ErrorMessage = "Card number must contain 14 to 19 digits.")]
    public string? CardNumber { get; set; }

    [Required]
    [Range(1, 12)]
    public int? ExpiryMonth { get; set; }

    [Required]
    [Range(1, 9999)]
    public int? ExpiryYear { get; set; }

    [Required]
    [RegularExpression("GBP|USD|EUR", ErrorMessage = "Currency must be GBP, USD, or EUR.")]
    public string? Currency { get; set; }

    [Required]
    public int? Amount { get; set; }

    [Required]
    [RegularExpression("[0-9]{3,4}", ErrorMessage = "CVV must contain 3 or 4 digits.")]
    public string? Cvv { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiryMonth is not (>= 1 and <= 12) || ExpiryYear is not (>= 1 and <= 9999))
        {
            yield break;
        }

        var timeProvider = validationContext.GetService(typeof(TimeProvider)) as TimeProvider
            ?? TimeProvider.System;
        var today = timeProvider.GetUtcNow();

        // Assumption: a card remains valid through the end of its expiry month.
        if (ExpiryYear < today.Year || (ExpiryYear == today.Year && ExpiryMonth < today.Month))
        {
            yield return new ValidationResult(
                "The card has expired.",
                new[] { nameof(ExpiryMonth), nameof(ExpiryYear) });
        }
    }
}
