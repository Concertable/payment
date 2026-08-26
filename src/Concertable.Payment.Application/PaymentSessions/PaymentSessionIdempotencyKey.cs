using System.Globalization;

namespace Concertable.Payment.Application.PaymentSessions;

internal readonly record struct PaymentSessionIdempotencyKey
{
    private readonly Guid operationId;
    private readonly Guid attemptId;
    private readonly long revision;

    public PaymentSessionIdempotencyKey(Guid operationId, Guid attemptId, long revision)
    {
        this.operationId = operationId;
        this.attemptId = attemptId;
        this.revision = revision;
    }

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"payment-session:{operationId:D}:{attemptId:D}:{revision}:create");
}
