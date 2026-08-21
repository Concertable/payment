using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionProviderRequest(
    Guid OperationId,
    Guid AttemptId,
    long Revision,
    PaymentSessionKind SessionKind,
    string OperationType,
    string ConsumerCorrelation,
    long? AmountMinor,
    Currency? Currency,
    PaymentSessionFundsRouting FundsRouting,
    string ProviderCustomerId,
    string? ProviderConnectedAccountId,
    IReadOnlyDictionary<string, string> Metadata);

internal sealed record PaymentSessionProviderResult(
    PaymentSessionProviderObjectKind ProviderObjectKind,
    string ProviderObjectId,
    string Status,
    DateTimeOffset ObservedAt,
    DateTimeOffset? CaptureBefore,
    ProviderFailureClassification? FailureClassification,
    bool IsExplicitConsumerCancellation,
    bool CanCancel,
    string? ClientSecret,
    string? ProviderRequestId,
    string? ProviderDiagnosticCode,
    string? ProviderDiagnosticMessage);

internal sealed record PaymentSessionExecution(
    PaymentOperationIdentity Identity,
    PaymentSessionKind Kind,
    PaymentOperationState State,
    string? ClientSecret,
    string? CustomerSessionSecret,
    string? CustomerToken);

internal sealed record PaymentSessionStatus(
    PaymentOperationIdentity Identity,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CaptureBefore,
    PaymentOperationFailure? Failure);

internal sealed class PaymentSessionProviderUnavailableException : Exception
{
    public PaymentSessionProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
