using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain.Lifecycle;
using Concertable.Payment.Domain.ProviderContract;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentSessionReconciliationService : IPaymentSessionReconciliationService
{
    private readonly IPaymentSessionAttemptRepository attemptRepository;
    private readonly IUnitOfWork unitOfWork;
    private readonly PaymentSessionStateMachine stateMachine;
    private readonly TimeProvider timeProvider;

    public PaymentSessionReconciliationService(
        IPaymentSessionAttemptRepository attemptRepository,
        IUnitOfWork unitOfWork,
        PaymentSessionStateMachine stateMachine,
        TimeProvider timeProvider)
    {
        this.attemptRepository = attemptRepository;
        this.unitOfWork = unitOfWork;
        this.stateMachine = stateMachine;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<PaymentSessionReconciliation, PaymentOperationError.ProviderUnavailable>> ReconcileAsync(
        PaymentSessionReconciliationRequest request,
        CancellationToken ct = default)
    {
        var attempt = request.Attempt;
        if (request.Provider is not { } provider)
        {
            var canonical = await RecordReconciliationRequiredAsync(
                attempt,
                timeProvider.GetUtcNow(),
                null,
                ct);
            if (canonical is null)
                return new PaymentOperationError.ProviderUnavailable();

            return new PaymentOperationError.ProviderUnavailable();
        }

        if (provider.ProviderObjectKind != attempt.ProviderObjectKind
            || attempt.ProviderObjectId is { } providerObjectId
            && !string.Equals(providerObjectId, provider.ProviderObjectId, StringComparison.Ordinal))
        {
            var canonical = await RecordReconciliationRequiredAsync(
                attempt,
                provider.ObservedAt,
                provider,
                ct);
            if (canonical is null)
                return new PaymentOperationError.ProviderUnavailable();

            return new PaymentOperationError.ProviderUnavailable();
        }

        attempt.BindProviderObject(provider.ProviderObjectId);
        var transition = EvaluateTransition(request.Operation, attempt, provider);

        if (transition.TryGetValue(out var applied))
        {
            attempt.ApplyTransition(
                request.Operation.SessionKind,
                applied,
                provider.ProviderRequestId,
                provider.ProviderDiagnosticCode,
                provider.ProviderDiagnosticMessage,
                request.EventEvidence?.ProviderEventId,
                request.EventEvidence?.ProviderEventCreatedAt);
        }
        else
        {
            attempt.RecordReconciliationRequired(
                provider.ObservedAt,
                provider.ProviderRequestId,
                provider.ProviderDiagnosticCode,
                provider.ProviderDiagnosticMessage);
        }

        var saved = await SaveAsync(attempt, provider.ProviderObjectId, ct);
        if (saved is null)
            return new PaymentOperationError.ProviderUnavailable();

        if (!saved.Committed)
            transition = EvaluateTransition(request.Operation, saved.Attempt, provider);

        return new PaymentSessionReconciliation(saved.Attempt, transition);
    }

    private async Task<PaymentSessionAttemptEntity?> RecordReconciliationRequiredAsync(
        PaymentSessionAttemptEntity attempt,
        DateTimeOffset attemptedAt,
        PaymentSessionProviderResult? provider,
        CancellationToken ct)
    {
        attempt.RecordReconciliationRequired(
            attemptedAt,
            provider?.ProviderRequestId,
            provider?.ProviderDiagnosticCode,
            provider?.ProviderDiagnosticMessage);
        return (await SaveAsync(attempt, attempt.ProviderObjectId, ct))?.Attempt;
    }

    private async Task<PaymentSessionSave?> SaveAsync(
        PaymentSessionAttemptEntity attempt,
        string? providerObjectId,
        CancellationToken ct)
    {
        var committed = await unitOfWork.TrySaveChangesAsync(static exception => exception is DbUpdateConcurrencyException, ct);
        if (committed)
            return new(attempt, true);

        var canonical = await attemptRepository.GetByAttemptIdAsync(attempt.AttemptId, ct);
        return canonical is not null
            && (providerObjectId is null
                || string.Equals(canonical.ProviderObjectId, providerObjectId, StringComparison.Ordinal))
            ? new(canonical, false)
            : null;
    }

    private Result<PaymentOperationTransition, PaymentOperationTransitionRejection> EvaluateTransition(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        PaymentSessionProviderResult provider)
    {
        var stripeObservation = new StripeProviderObservation(
            StripeProviderContractBaseline.ApiVersion,
            provider.ProviderObjectKind == PaymentSessionProviderObjectKind.PaymentIntent
                ? StripeProviderObjectKind.PaymentIntent
                : StripeProviderObjectKind.SetupIntent,
            provider.ProviderObjectId,
            operation.OperationId,
            attempt.AttemptId,
            attempt.Revision,
            operation.SessionKind,
            provider.Status,
            provider.ObservedAt,
            provider.CaptureBefore,
            provider.FailureClassification,
            provider.IsExplicitConsumerCancellation);

        var normalized = stripeObservation.ToNormalized(attempt.State);
        if (!normalized.TryGetValue(out var observation))
        {
            normalized.TryGetError(out var normalizationRejection);
            return normalizationRejection!;
        }

        if (attempt.LastObservedAt is { } lastObservedAt)
        {
            if (observation.ObservedAt < lastObservedAt)
                return Reject(attempt, PaymentOperationTransitionRejectionReason.StaleObservation, observation.State);
            if (observation.ObservedAt == lastObservedAt
                && !string.Equals(observation.ProviderStatus, attempt.LastProviderStatus, StringComparison.Ordinal))
                return Reject(attempt, PaymentOperationTransitionRejectionReason.AmbiguousObservationOrder, observation.State);
        }

        return stateMachine.Evaluate(attempt.State, observation);
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentSessionAttemptEntity attempt,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState observedState) =>
        new(reason, attempt.State, observedState);

    private sealed record PaymentSessionSave(
        PaymentSessionAttemptEntity Attempt,
        bool Committed);
}
