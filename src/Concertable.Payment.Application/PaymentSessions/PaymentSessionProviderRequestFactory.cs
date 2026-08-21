namespace Concertable.Payment.Application.PaymentSessions;

internal static class PaymentSessionProviderRequestFactory
{
    public static PaymentSessionProviderRequest Create(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt) =>
        new(
            operation.OperationId,
            attempt.AttemptId,
            attempt.Revision,
            operation.SessionKind,
            operation.OperationType,
            operation.ConsumerCorrelation,
            operation.AmountMinor,
            operation.Currency,
            operation.FundsRouting,
            operation.ProviderCustomerId,
            operation.ProviderConnectedAccountId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operation_id"] = operation.OperationId.ToString("D"),
                ["attempt_id"] = attempt.AttemptId.ToString("D"),
                ["revision"] = attempt.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["session_kind"] = operation.SessionKind.ToString(),
                ["type"] = operation.OperationType,
                ["correlation"] = operation.ConsumerCorrelation
            });
}
