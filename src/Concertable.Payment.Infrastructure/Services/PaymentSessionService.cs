using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.ProviderContract;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentSessionService : IPaymentSessionService
{
    private readonly IPaymentSessionOperationRepository operationRepository;
    private readonly IPaymentSessionAttemptRepository attemptRepository;
    private readonly IStripeSessionClient stripeSessionClient;
    private readonly TimeProvider timeProvider;

    public PaymentSessionService(
        IPaymentSessionOperationRepository operationRepository,
        IPaymentSessionAttemptRepository attemptRepository,
        IStripeSessionClient stripeSessionClient,
        TimeProvider timeProvider)
    {
        this.operationRepository = operationRepository;
        this.attemptRepository = attemptRepository;
        this.stripeSessionClient = stripeSessionClient;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateOrReplayAsync(
        PaymentSessionSpecification specification,
        CancellationToken ct = default)
    {
        var reservation = await operationRepository.ReserveInitialAsync(
            specification,
            timeProvider.GetUtcNow(),
            ct);
        if (reservation.Disposition == PaymentSessionReservationDisposition.Conflict)
            return new PaymentOperationError.OperationConflict();
        if (reservation.Operation is null || reservation.Attempt is null)
            return new PaymentOperationError.Unknown();

        return await ExecuteAsync(reservation.Operation, reservation.Attempt, ct);
    }

    public async Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        CancellationToken ct = default)
    {
        var operation = await operationRepository.GetByOperationIdAsync(operationId, ct);
        if (operation is null)
            return new PaymentOperationError.Unknown();

        var current = operation.CurrentAttempt;
        var duplicateRetry = current.Revision == expectedRevision + 1
            && current.PredecessorAttemptId == expectedAttemptId;
        if (!duplicateRetry)
        {
            if (current.AttemptId != expectedAttemptId
                || current.Revision != expectedRevision
                || current.ProviderObjectId is null)
            {
                return new PaymentOperationError.OperationConflict();
            }

            try
            {
                var provider = await stripeSessionClient.RetrieveAsync(
                    current.ProviderObjectKind,
                    current.ProviderObjectId,
                    ct);
                if (provider.CanCancel)
                {
                    await stripeSessionClient.CancelAsync(
                        current.ProviderObjectKind,
                        current.ProviderObjectId,
                        ct);
                }
                else if (!string.Equals(provider.Status, "canceled", StringComparison.Ordinal))
                {
                    return new PaymentOperationError.ProviderUnavailable();
                }
            }
            catch (PaymentSessionProviderUnavailableException)
            {
                return new PaymentOperationError.ProviderUnavailable();
            }
        }

        var reservation = await operationRepository.ReserveNextAttemptAsync(
            operationId,
            expectedAttemptId,
            expectedRevision,
            timeProvider.GetUtcNow(),
            ct);
        if (reservation.Disposition == PaymentSessionReservationDisposition.Conflict)
            return new PaymentOperationError.OperationConflict();
        if (reservation.Disposition == PaymentSessionReservationDisposition.NotRetryable)
            return new PaymentOperationError.OperationConflict();
        if (reservation.Operation is null || reservation.Attempt is null)
            return new PaymentOperationError.Unknown();

        return await ExecuteAsync(reservation.Operation, reservation.Attempt, ct);
    }

    public async Task<Result<PaymentSessionStatus, PaymentOperationError>> RefreshAsync(
        Guid operationId,
        CancellationToken ct = default)
    {
        var operation = await operationRepository.GetByOperationIdAsync(operationId, ct);
        if (operation is null)
            return new PaymentOperationError.Unknown();

        var attempt = operation.CurrentAttempt;
        if (attempt.ProviderObjectId is null)
            return ToStatus(attempt);

        try
        {
            var provider = await stripeSessionClient.RetrieveAsync(
                attempt.ProviderObjectKind,
                attempt.ProviderObjectId,
                ct);
            var applied = await ApplyAsync(operation, attempt, provider, ct);
            if (!applied.TryGetValue(out var canonical))
                return new PaymentOperationError.ProviderUnavailable();

            return ToStatus(canonical);
        }
        catch (PaymentSessionProviderUnavailableException)
        {
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    private async Task<Result<PaymentSessionExecution, PaymentOperationError>> ExecuteAsync(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        CancellationToken ct)
    {
        try
        {
            PaymentSessionProviderResult provider;
            if (attempt.ProviderObjectId is null)
            {
                var request = PaymentSessionProviderRequestFactory.Create(operation, attempt);
                provider = await stripeSessionClient.CreateAsync(
                    request,
                    PaymentSessionIdempotencyKeyGenerator.Create(
                        operation.OperationId,
                        attempt.AttemptId,
                        attempt.Revision),
                    ct);
            }
            else
            {
                provider = await stripeSessionClient.RetrieveAsync(
                    attempt.ProviderObjectKind,
                    attempt.ProviderObjectId,
                    ct);
            }

            var applied = await ApplyAsync(operation, attempt, provider, ct);
            if (!applied.TryGetValue(out var canonical))
                return new PaymentOperationError.ProviderUnavailable();

            var customerSessionSecret = await stripeSessionClient.CreateCustomerSessionAsync(
                operation.ProviderCustomerId,
                ct);
            return new PaymentSessionExecution(
                new(operation.OperationId, canonical.AttemptId, canonical.Revision),
                operation.SessionKind,
                canonical.State,
                provider.ClientSecret,
                customerSessionSecret);
        }
        catch (PaymentSessionProviderUnavailableException)
        {
            return new PaymentOperationError.ProviderUnavailable();
        }
    }

    private async Task<Result<PaymentSessionAttemptEntity, PaymentOperationError>> ApplyAsync(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        PaymentSessionProviderResult provider,
        CancellationToken ct)
    {
        if (provider.ProviderObjectKind != attempt.ProviderObjectKind)
            return new PaymentOperationError.ProviderUnavailable();
        if (attempt.ProviderObjectId is { } providerObjectId
            && !string.Equals(providerObjectId, provider.ProviderObjectId, StringComparison.Ordinal))
        {
            return new PaymentOperationError.ProviderUnavailable();
        }

        attempt.BindProviderObject(provider.ProviderObjectId);
        var transition = StripeOperationTransitionEvaluator.Evaluate(
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

        if (transition.TryGetValue(out var applied))
        {
            attempt.ApplyTransition(
                applied,
                provider.ProviderRequestId,
                provider.ProviderDiagnosticCode,
                provider.ProviderDiagnosticMessage);
        }
        else
        {
            attempt.RecordReconciliationRequired(
                provider.ObservedAt,
                provider.ProviderRequestId,
                provider.ProviderDiagnosticCode,
                provider.ProviderDiagnosticMessage);
        }

        try
        {
            await attemptRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            attemptRepository.Detach(attempt);
            var canonical = await attemptRepository.GetByAttemptIdAsync(attempt.AttemptId, ct);
            if (canonical is null
                || !string.Equals(
                    canonical.ProviderObjectId,
                    provider.ProviderObjectId,
                    StringComparison.Ordinal))
            {
                return new PaymentOperationError.ProviderUnavailable();
            }

            attempt = canonical;
        }

        return transition.TryGetValue(out _) ? attempt : new PaymentOperationError.ProviderUnavailable();
    }

    private static PaymentSessionStatus ToStatus(PaymentSessionAttemptEntity attempt) =>
        new(
            new(attempt.OperationId, attempt.AttemptId, attempt.Revision),
            attempt.State,
            attempt.CaptureBefore,
            attempt.FailureCode is { } code ? PaymentOperationFailure.FromCode(code) : null);
}
