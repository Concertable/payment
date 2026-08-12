namespace Concertable.Payment.Domain.Entities;

internal sealed class FinancialOperationEntity
{
    private FinancialOperationEntity() { }

    private FinancialOperationEntity(
        Guid id,
        int bookingId,
        FinancialOperationType type,
        string requestFingerprint,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("Financial operation id is required.");
        if (bookingId <= 0)
            throw new DomainException("Financial operation booking id must be positive.");
        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new DomainException("Financial operation request fingerprint is required.");

        Id = id;
        BookingId = bookingId;
        Type = type;
        RequestFingerprint = requestFingerprint;
        Status = FinancialOperationStatus.Pending;
        CreatedAt = createdAt;
        LastAttemptedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public int BookingId { get; private set; }
    public FinancialOperationType Type { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public FinancialOperationStatus Status { get; private set; }
    public string? ReferenceId { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastAttemptedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static FinancialOperationEntity Create(
        Guid id,
        int bookingId,
        FinancialOperationType type,
        string requestFingerprint,
        DateTimeOffset createdAt) =>
        new(id, bookingId, type, requestFingerprint, createdAt);

    public void EnsureMatches(int bookingId, FinancialOperationType type, string requestFingerprint)
    {
        if (BookingId != bookingId || Type != type || RequestFingerprint != requestFingerprint)
            throw new InvalidOperationException($"Financial operation {Id} was reused with a different request.");
    }

    public void RecordAttempt(DateTimeOffset attemptedAt)
    {
        if (Status != FinancialOperationStatus.Pending)
            throw new InvalidOperationException($"Terminal financial operation {Id} cannot be retried.");

        LastAttemptedAt = attemptedAt;
    }

    public void Succeed(string referenceId, DateTimeOffset completedAt)
    {
        if (Status != FinancialOperationStatus.Pending)
            throw new InvalidOperationException($"Terminal financial operation {Id} cannot succeed again.");
        if (string.IsNullOrWhiteSpace(referenceId))
            throw new DomainException("Financial operation reference id is required.");

        ReferenceId = referenceId;
        Status = FinancialOperationStatus.Succeeded;
        LastAttemptedAt = completedAt;
        CompletedAt = completedAt;
    }

    public void Reject(string code, string message, DateTimeOffset completedAt)
    {
        if (Status != FinancialOperationStatus.Pending)
            throw new InvalidOperationException($"Terminal financial operation {Id} cannot be rejected again.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message))
            throw new DomainException("Financial operation rejection details are required.");

        FailureCode = code;
        FailureMessage = message;
        Status = FinancialOperationStatus.Rejected;
        LastAttemptedAt = completedAt;
        CompletedAt = completedAt;
    }
}
