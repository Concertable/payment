using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain.ProviderContract;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentSessionReconciliationService : IPaymentSessionReconciliationService
{
    private readonly IPaymentSessionAttemptRepository attemptRepository;
    private readonly TimeProvider timeProvider;

    public PaymentSessionReconciliationService(
        IPaymentSessionAttemptRepository attemptRepository,
        TimeProvider timeProvider)
    {
        this.attemptRepository = attemptRepository;
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
        try
        {
            await attemptRepository.SaveChangesAsync(ct);
            return new(attempt, true);
        }
        catch (DbUpdateConcurrencyException)
        {
            attemptRepository.Detach(attempt);
            var canonical = await attemptRepository.GetByAttemptIdAsync(attempt.AttemptId, ct);
            return canonical is not null
                && (providerObjectId is null
                    || string.Equals(canonical.ProviderObjectId, providerObjectId, StringComparison.Ordinal))
                ? new(canonical, false)
                : null;
        }
    }

    private static Result<PaymentOperationTransition, PaymentOperationTransitionRejection> EvaluateTransition(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        PaymentSessionProviderResult provider) =>
        StripeOperationTransitionEvaluator.Evaluate(
            attempt.ToProviderAttempt(operation.SessionKind, operation.RequestFingerprint),
            new StripeProviderObservation(
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
                provider.IsExplicitConsumerCancellation));

    private sealed record PaymentSessionSave(
        PaymentSessionAttemptEntity Attempt,
        bool Committed);
}
