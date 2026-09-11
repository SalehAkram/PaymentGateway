namespace PaymentGateway.Core.Exceptions;

public enum BankFailure
{
    Unavailable,
    Timeout,
    InvalidResponse
}

public sealed class BankException : Exception
{
    public BankFailure Failure { get; }

    public BankException(BankFailure failure)
        : base($"Bank operation failed: {failure}.")
    {
        Failure = failure;
    }
}
