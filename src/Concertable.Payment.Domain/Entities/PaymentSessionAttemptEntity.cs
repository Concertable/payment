using Concertable.Payment.Domain.Events;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Domain.Entities;

internal sealed class PaymentSessionAttemptEntity : IEventRaiser
{
    private readonly EventRaiser events = new();

    private PaymentSessionAttemptEntity() { }

    private PaymentSessionAttemptEntity(
        Guid attemptId,
        Guid operationId,
        long revision,
        Guid? predecessorAttemptId,
        PaymentSessionProviderObjectKind providerObjectKind,
        DateTimeOffset createdAt)
    {
        if (attemptId == Guid.Empty)
            throw new DomainException("Payment session attempt id is required.");
        if (attemptId.Version != 7)
            throw new DomainException("Payment session attempt id must be UUIDv7.");
        if (operationId == Guid.Empty)
            throw new DomainException("Payment session operation id is required.");
        if (revision <= 0)
            throw new DomainException("Payment session attempt revision must be positive.");
        if (revision == 1 && predecessorAttemptId is not null)
            throw new DomainException("The first payment session attempt cannot have a predecessor.");
        if (revision > 1 && predecessorAttemptId is null)
            throw new DomainException("A revised payment session attempt requires a predecessor.");
        if (!Enum.IsDefined(providerObjectKind))
            throw new DomainException("Payment session provider object kind is invalid.");

        AttemptId = attemptId;
        OperationId = operationId;
        Revision = revision;
        PredecessorAttemptId = predecessorAttemptId;
        ProviderObjectKind = providerObjectKind;
        State = PaymentOperationState.Creating;
        CreatedAt = createdAt;
        LastAttemptedAt = createdAt;
    }

    public Guid AttemptId { get; private set; }
    public Guid OperationId { get; private set; }
    public long Revision { get; private set; }
    public Guid? PredecessorAttemptId { get; private set; }
    public PaymentSessionProviderObjectKind ProviderObjectKind { get; private set; }
    public string? ProviderObjectId { get; private set; }
    public PaymentOperationState State { get; private set; }
    public string? LastProviderStatus { get; private set; }
    public PaymentOperationFailureCode? FailureCode { get; private set; }
    public string? ProviderRequestId { get; private set; }
    public string? ProviderDiagnosticCode { get; private set; }
    public string? ProviderDiagnosticMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastAttemptedAt { get; private set; }
    public DateTimeOffset? LastObservedAt { get; private set; }
    public DateTimeOffset? NextReconcileAt { get; private set; }
    public DateTimeOffset? TerminalAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? CaptureBefore { get; private set; }
    public string? LastProviderEventId { get; private set; }
    public DateTimeOffset? LastProviderEventCreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public IReadOnlyList<IDomainEvent> DomainEvents => events.DomainEvents;

    public void ClearDomainEvents() => events.Clear();

    internal static PaymentSessionAttemptEntity Create(
        Guid attemptId,
        Guid operationId,
        long revision,
        Guid? predecessorAttemptId,
        PaymentSessionProviderObjectKind providerObjectKind,
        DateTimeOffset createdAt) =>
        new(
            attemptId,
            operationId,
            revision,
            predecessorAttemptId,
            providerObjectKind,
            createdAt);

    public void BindProviderObject(string providerObjectId)
    {
        if (string.IsNullOrWhiteSpace(providerObjectId))
            throw new DomainException("Payment session provider object id is required.");

        providerObjectId = providerObjectId.Trim();
        if (providerObjectId.Length > 100)
            throw new DomainException("Payment session provider object id cannot exceed 100 characters.");

        if (ProviderObjectId is not null
            && !string.Equals(ProviderObjectId, providerObjectId, StringComparison.Ordinal))
        {
            throw new DomainException("Payment session provider binding is immutable.");
        }

        ProviderObjectId = providerObjectId;
    }

    internal void ApplyTransition(
        PaymentSessionKind sessionKind,
        PaymentOperationTransition transition,
        string? providerRequestId = null,
        string? providerDiagnosticCode = null,
        string? providerDiagnosticMessage = null,
        string? providerEventId = null,
        DateTimeOffset? providerEventCreatedAt = null)
    {
        if (ProviderObjectId is null)
            throw new DomainException("A payment session attempt must be provider-bound before observation.");
        if (transition.Disposition == PaymentOperationTransitionDisposition.Duplicate)
            return;

        var observableChange = State != transition.State
            || FailureCode != transition.Failure?.Code
            || CaptureBefore != transition.CaptureBefore;

        LastAttemptedAt = transition.ObservedAt;
        State = transition.State;
        LastProviderStatus = RequiredDiagnostic(transition.ProviderStatus, "provider status", 100);
        LastObservedAt = transition.ObservedAt;
        NextReconcileAt = null;
        CaptureBefore = transition.CaptureBefore;
        FailureCode = transition.Failure?.Code;
        ProviderRequestId = OptionalDiagnostic(providerRequestId, "provider request id", 100);
        ProviderDiagnosticCode = OptionalDiagnostic(providerDiagnosticCode, "provider diagnostic code", 100);
        ProviderDiagnosticMessage = OptionalDiagnostic(
            providerDiagnosticMessage,
            "provider diagnostic message",
            1000);
        LastProviderEventId = OptionalDiagnostic(providerEventId, "provider event id", 100);
        LastProviderEventCreatedAt = providerEventCreatedAt;
        TerminalAt = transition.TerminalDisposition == PaymentOperationTerminalDisposition.NonTerminal
            ? null
            : transition.ObservedAt;

        if (observableChange)
        {
            events.Raise(new PaymentOperationStateChangedDomainEvent(
                new PaymentOperationIdentity(OperationId, AttemptId, Revision),
                sessionKind,
                transition.State,
                transition.TerminalDisposition,
                transition.RetryDisposition,
                transition.Failure,
                ExpiresAt,
                transition.CaptureBefore,
                transition.ObservedAt));
        }
    }

    internal void RecordReconciliationRequired(
        DateTimeOffset attemptedAt,
        string? providerRequestId,
        string? providerDiagnosticCode,
        string? providerDiagnosticMessage)
    {
        LastAttemptedAt = attemptedAt;
        NextReconcileAt = attemptedAt;
        ProviderRequestId = OptionalDiagnostic(providerRequestId, "provider request id", 100);
        ProviderDiagnosticCode = OptionalDiagnostic(providerDiagnosticCode, "provider diagnostic code", 100);
        ProviderDiagnosticMessage = OptionalDiagnostic(
            providerDiagnosticMessage,
            "provider diagnostic message",
            1000);
    }

    internal PaymentProviderAttempt ToProviderAttempt(
        PaymentSessionKind sessionKind,
        string requestFingerprint)
    {
        if (ProviderObjectId is null)
            throw new DomainException("A payment session attempt must be provider-bound before evaluation.");

        return new(
            OperationId,
            AttemptId,
            Revision,
            sessionKind switch
            {
                PaymentSessionKind.Payment => new PaymentProviderOperationContext.Payment(),
                PaymentSessionKind.Authorization => new PaymentProviderOperationContext.Authorization(),
                PaymentSessionKind.PaymentMethodSetup => new PaymentProviderOperationContext.PaymentMethodSetup(),
                PaymentSessionKind.PaymentMethodVerification =>
                    new PaymentProviderOperationContext.PaymentMethodVerification(),
                _ => throw new DomainException("Payment session kind is invalid.")
            },
            ProviderObjectId,
            State,
            requestFingerprint,
            LastProviderStatus,
            LastObservedAt,
            CaptureBefore,
            FailureCode is { } failureCode ? PaymentOperationFailure.FromCode(failureCode) : null);
    }

    private static string RequiredDiagnostic(string value, string name, int maxLength) =>
        OptionalDiagnostic(value, name, maxLength)
        ?? throw new DomainException($"Payment session {name} is required.");

    private static string? OptionalDiagnostic(string? value, string name, int maxLength)
    {
        if (value is null)
            return null;

        var normalized = value.Trim();
        if (normalized.Length == 0)
            throw new DomainException($"Payment session {name} is required when supplied.");
        if (normalized.Length > maxLength)
            throw new DomainException($"Payment session {name} cannot exceed {maxLength} characters.");

        return normalized;
    }
}
