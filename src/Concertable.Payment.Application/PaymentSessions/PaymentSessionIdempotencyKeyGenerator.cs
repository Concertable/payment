namespace Concertable.Payment.Application.PaymentSessions;

internal static class PaymentSessionIdempotencyKeyGenerator
{
    public static string Create(Guid operationId, Guid attemptId, long revision) =>
        $"payment-session:{operationId:D}:{attemptId:D}:{revision}:create";
}
