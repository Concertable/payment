using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Domain.Entities;

internal sealed class PaymentSessionOperationEntity
{
    private readonly List<PaymentSessionAttemptEntity> attempts = [];

    private PaymentSessionOperationEntity() { }

    private PaymentSessionOperationEntity(
        PaymentSessionDefinition specification,
        PaymentSessionFingerprint fingerprint,
        Guid attemptId,
        DateTimeOffset createdAt)
    {
        OperationId = specification.OperationId;
        SessionKind = specification.SessionKind;
        Session = specification.Session;
        OperationType = specification.OperationType;
        ClientReference = specification.ClientReference;
        PayerOwnerKey = specification.PayerOwnerKey;
        PayeeOwnerKey = specification.PayeeOwnerKey;
        AmountMinor = specification.AmountMinor;
        Currency = specification.Currency;
        FundsRouting = specification.FundsRouting;
        PaymentMethodId = specification.PaymentMethodId;
        ProviderCustomerId = specification.ProviderCustomerId;
        ProviderConnectedAccountId = specification.ProviderConnectedAccountId;
        MandateTermsVersion = specification.MandateTermsVersion;
        MandateAcceptedAt = specification.MandateTermsVersion is null ? null : createdAt;
        FingerprintVersion = fingerprint.Version;
        RequestFingerprint = fingerprint.Value;
        CurrentRevision = 1;
        CreatedAt = createdAt;
        attempts.Add(PaymentSessionAttemptEntity.Create(
            attemptId,
            OperationId,
            CurrentRevision,
            null,
            ProviderObjectKind(specification.SessionKind),
            createdAt));
    }

    public Guid OperationId { get; private set; }
    public PaymentSessionKind SessionKind { get; private set; }
    public PaymentSession Session { get; private set; }
    public string OperationType { get; private set; } = null!;
    public string ClientReference { get; private set; } = null!;
    public string PayerOwnerKey { get; private set; } = null!;
    public string? PayeeOwnerKey { get; private set; }
    public long? AmountMinor { get; private set; }
    public Currency? Currency { get; private set; }
    public PaymentSessionFundsRouting FundsRouting { get; private set; }
    public string? PaymentMethodId { get; private set; }
    public string ProviderCustomerId { get; private set; } = null!;
    public string? ProviderConnectedAccountId { get; private set; }
    public string? MandateTermsVersion { get; private set; }
    public DateTimeOffset? MandateAcceptedAt { get; private set; }
    public int FingerprintVersion { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public long CurrentRevision { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public IReadOnlyList<PaymentSessionAttemptEntity> Attempts => attempts.AsReadOnly();
    public PaymentSessionAttemptEntity CurrentAttempt =>
        attempts.Single(attempt => attempt.Revision == CurrentRevision);

    internal static PaymentSessionOperationEntity Create(
        PaymentSessionDefinition specification,
        Guid attemptId,
        DateTimeOffset createdAt) =>
        new(
            specification,
            PaymentSessionFingerprint.Create(specification),
            attemptId,
            createdAt);

    internal PaymentSessionReservation EvaluateInitialReservation(PaymentSessionFingerprint fingerprint) =>
        FingerprintVersion == fingerprint.Version
        && string.Equals(RequestFingerprint, fingerprint.Value, StringComparison.Ordinal)
            ? new(PaymentSessionReservationDisposition.Replayed, this, CurrentAttempt)
            : new(PaymentSessionReservationDisposition.Conflict, this, null);

    internal PaymentSessionReservation ReserveNextAttempt(
        Guid expectedAttemptId,
        long expectedRevision,
        Guid proposedAttemptId,
        DateTimeOffset createdAt)
    {
        var current = CurrentAttempt;
        if (expectedRevision is > 0 and < long.MaxValue
            && current.Revision == expectedRevision + 1
            && current.PredecessorAttemptId == expectedAttemptId)
        {
            return new(PaymentSessionReservationDisposition.Replayed, this, current);
        }

        if (expectedRevision <= 0
            || current.AttemptId != expectedAttemptId
            || current.Revision != expectedRevision)
        {
            return new(PaymentSessionReservationDisposition.Conflict, this, null);
        }

        if (current.ProviderObjectId is null)
            return new(PaymentSessionReservationDisposition.NotRetryable, this, null);

        var decision = PaymentOperationRetryEvaluator.Evaluate(
            current.ToProviderAttempt(SessionKind, RequestFingerprint),
            new(
                PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                RequestFingerprint,
                proposedAttemptId));

        if (!decision.TryGetValue(out var retry)
            || retry.Disposition != PaymentOperationRetryDisposition.CreateNewAttempt)
        {
            return new(PaymentSessionReservationDisposition.NotRetryable, this, null);
        }

        var next = PaymentSessionAttemptEntity.Create(
            retry.AttemptId,
            OperationId,
            retry.Revision,
            current.AttemptId,
            current.ProviderObjectKind,
            createdAt);
        attempts.Add(next);
        CurrentRevision = retry.Revision;
        return new(PaymentSessionReservationDisposition.Created, this, next);
    }

    private static PaymentSessionProviderObjectKind ProviderObjectKind(PaymentSessionKind sessionKind) =>
        sessionKind switch
        {
            PaymentSessionKind.Payment or PaymentSessionKind.Authorization =>
                PaymentSessionProviderObjectKind.PaymentIntent,
            PaymentSessionKind.PaymentMethodSetup or PaymentSessionKind.PaymentMethodVerification =>
                PaymentSessionProviderObjectKind.SetupIntent,
            _ => throw new DomainException("Payment session kind is invalid.")
        };
}
