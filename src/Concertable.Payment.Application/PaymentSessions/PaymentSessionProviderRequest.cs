using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionProviderRequest(
    Guid OperationId,
    Guid AttemptId,
    long Revision,
    PaymentSessionKind SessionKind,
    PaymentSession Session,
    string OperationType,
    string ClientReference,
    long? AmountMinor,
    Currency? Currency,
    PaymentSessionFundsRouting FundsRouting,
    string? PaymentMethodId,
    string ProviderCustomerId,
    string? ProviderConnectedAccountId,
    IReadOnlyDictionary<string, string> Metadata)
{
    internal static PaymentSessionProviderRequest Create(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt) =>
        new(
            operation.OperationId,
            attempt.AttemptId,
            attempt.Revision,
            operation.SessionKind,
            operation.Session,
            operation.OperationType,
            operation.ClientReference,
            operation.AmountMinor,
            operation.Currency,
            operation.FundsRouting,
            operation.PaymentMethodId,
            operation.ProviderCustomerId,
            operation.ProviderConnectedAccountId,
            MetadataOf(operation, attempt));

    private static Dictionary<string, string> MetadataOf(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PaymentMetadataKeys.OperationId] = operation.OperationId.ToString("D"),
            ["attempt_id"] = attempt.AttemptId.ToString("D"),
            ["revision"] = attempt.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["session_kind"] = operation.SessionKind.ToString(),
            [PaymentMetadataKeys.Type] = operation.OperationType,
            ["correlation"] = operation.ClientReference,
            [PaymentMetadataKeys.OperationType] = operation.OperationType,
            [PaymentMetadataKeys.ClientReference] = operation.ClientReference,
            [PaymentMetadataKeys.PayerOwnerId] = operation.PayerOwnerKey
        };
        if (operation.PayeeOwnerKey is { } payeeOwnerKey)
            metadata[PaymentMetadataKeys.PayeeOwnerId] = payeeOwnerKey;

        return metadata;
    }
}
