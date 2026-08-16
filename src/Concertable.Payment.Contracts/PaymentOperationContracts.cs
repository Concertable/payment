using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Contracts;

public enum PaymentSessionKind
{
    Payment = 1,
    Authorization = 2,
    PaymentMethodSetup = 3,
    PaymentMethodVerification = 4
}

public enum PaymentOperationState
{
    Creating = 1,
    RequiresPaymentMethod = 2,
    RequiresConfirmation = 3,
    RequiresAction = 4,
    Processing = 5,
    Authorized = 6,
    Succeeded = 7,
    Canceled = 8,
    Failed = 9
}

public enum PaymentOperationTerminalDisposition
{
    NonTerminal = 1,
    AttemptTerminal = 2,
    OperationTerminal = 3
}

public enum PaymentOperationRetryDisposition
{
    ContinueCurrentAttempt = 1,
    RetryCurrentAttempt = 2,
    CreateNewAttempt = 3,
    CreateNewOperation = 4,
    Reconcile = 5,
    NotRetryable = 6
}

public enum PaymentOperationFailureCode
{
    PaymentMethodRequired = 1,
    AuthenticationRequired = 2,
    Declined = 3,
    Expired = 4,
    Canceled = 5,
    OperationConflict = 6,
    ProviderUnavailable = 7,
    Unknown = 8
}

public sealed record PaymentOperationIdentity(
    Guid OperationId,
    Guid AttemptId,
    long Revision);

public sealed record PaymentOperationFailure(
    PaymentOperationFailureCode Code,
    string Message)
{
    public static PaymentOperationFailure FromCode(PaymentOperationFailureCode code)
    {
        PaymentOperationError error = code switch
        {
            PaymentOperationFailureCode.PaymentMethodRequired => new PaymentOperationError.PaymentMethodRequired(),
            PaymentOperationFailureCode.AuthenticationRequired => new PaymentOperationError.AuthenticationRequired(),
            PaymentOperationFailureCode.Declined => new PaymentOperationError.Declined(),
            PaymentOperationFailureCode.Expired => new PaymentOperationError.Expired(),
            PaymentOperationFailureCode.Canceled => new PaymentOperationError.Canceled(),
            PaymentOperationFailureCode.OperationConflict => new PaymentOperationError.OperationConflict(),
            PaymentOperationFailureCode.ProviderUnavailable => new PaymentOperationError.ProviderUnavailable(),
            PaymentOperationFailureCode.Unknown => new PaymentOperationError.Unknown(),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };

        return new PaymentOperationFailure(code, error.Definition.Message);
    }
}
