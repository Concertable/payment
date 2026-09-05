using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Application.Provider;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentSessionService : IPaymentSessionService
{
    private readonly IPaymentSessionOperationRepository operationRepository;
    private readonly IPayoutAccountRepository payoutAccountRepository;
    private readonly IPaymentSessionReconciliationService reconciliationService;
    private readonly IStripeSessionClient stripeSessionClient;
    private readonly IPaymentOperationResolver paymentOperationResolver;
    private readonly TimeProvider timeProvider;

    public PaymentSessionService(
        IPaymentSessionOperationRepository operationRepository,
        IPayoutAccountRepository payoutAccountRepository,
        IPaymentSessionReconciliationService reconciliationService,
        IStripeSessionClient stripeSessionClient,
        IPaymentOperationResolver paymentOperationResolver,
        TimeProvider timeProvider)
    {
        this.operationRepository = operationRepository;
        this.payoutAccountRepository = payoutAccountRepository;
        this.reconciliationService = reconciliationService;
        this.stripeSessionClient = stripeSessionClient;
        this.paymentOperationResolver = paymentOperationResolver;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<PaymentSessionExecution, PaymentOperationError>> SetupPaymentMethodAsync(
        PaymentMethodSetupRequest request,
        CancellationToken ct = default)
    {
        if (!TryValidate(request.Reference, out var reference))
            return new PaymentOperationError.Unknown();

        if (request.Kind is not (PaymentSessionKind.PaymentMethodSetup
            or PaymentSessionKind.PaymentMethodVerification))
        {
            return new PaymentOperationError.Unknown();
        }

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(request.PayerOwnerId, ct);
        if (payer?.StripeCustomerId is null)
            return new PaymentOperationError.ProviderUnavailable();

        var existing = await operationRepository.GetByReferenceAsync(reference, ct);
        var specification = PaymentSessionDefinition.Create(
            existing?.OperationId ?? Guid.CreateVersion7(timeProvider.GetUtcNow()),
            request.Kind,
            PaymentSession.OnSession,
            reference.OperationType,
            reference.ClientReference,
            request.PayerOwnerId.ToString("D"),
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            payer.StripeCustomerId,
            null,
            request.MandateTermsVersion);
        return await CreateAsync(specification, ct);
    }

    public async Task<UnitResult<PaymentOperationError>> ValidatePaymentMethodAsync(
        PaymentMethodValidationRequest request,
        CancellationToken ct = default)
    {
        if (!TryValidate(request.Reference, out var reference))
            return new PaymentOperationError.Unknown();

        var paymentMethod = await paymentOperationResolver.ResolvePaymentMethodAsync(
            reference,
            request.PayerOwnerId,
            ct);
        return paymentMethod.TryGetError(out var error)
            ? error
            : new Success();
    }

    public async Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateAsync(
        PaymentSessionOperationRequest request,
        CancellationToken ct = default)
    {
        if (!TryValidate(request.Reference, out var reference))
            return new PaymentOperationError.Unknown();

        var payer = await payoutAccountRepository.GetByOwnerIdAsync(request.PayerOwnerId, ct);
        if (payer?.StripeCustomerId is null)
            return new PaymentOperationError.ProviderUnavailable();

        string? providerConnectedAccountId = null;
        if (request.FundsRouting == PaymentSessionFundsRouting.Destination)
        {
            if (request.PayeeOwnerId is not { } payeeOwnerId)
                return new PaymentOperationError.Unknown();

            var payee = await payoutAccountRepository.GetByOwnerIdAsync(payeeOwnerId, ct);
            if (payee?.StripeAccountId is null)
                return new PaymentOperationError.ProviderUnavailable();

            providerConnectedAccountId = payee.StripeAccountId;
        }

        try
        {
            var specification = PaymentSessionDefinition.Create(
                request.OperationId,
                request.Kind,
                request.Session,
                reference.OperationType,
                reference.ClientReference,
                request.PayerOwnerId.ToString("D"),
                request.PayeeOwnerId?.ToString("D"),
                request.AmountMinor,
                request.Currency,
                request.FundsRouting,
                null,
                payer.StripeCustomerId,
                providerConnectedAccountId,
                request.MandateTermsVersion);
            return await CreateAsync(specification, ct);
        }
        catch (DomainException)
        {
            return new PaymentOperationError.Unknown();
        }
    }

    public async Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateAsync(
        PaymentSessionDefinition specification,
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
        PaymentSessionRetryRequest request,
        CancellationToken ct = default) =>
        await RetryAsync(
            request.OperationId,
            request.ExpectedAttemptId,
            request.ExpectedRevision,
            request.OwnerId,
            ct);

    internal Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        CancellationToken ct = default) =>
        RetryAsync(operationId, expectedAttemptId, expectedRevision, null, ct);

    private async Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        Guid? ownerId,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetByOperationIdAsync(operationId, ct);
        if (operation is null)
            return new PaymentOperationError.Unknown();
        if (ownerId is { } scope && !IsPayer(operation, scope))
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

            var retrieved = await stripeSessionClient.RetrieveAsync(
                current.ProviderObjectKind,
                current.ProviderObjectId,
                ct);
            if (!retrieved.TryGetValue(out var provider))
            {
                await ReconcileAsync(operation, current, null, ct);
                return new PaymentOperationError.ProviderUnavailable();
            }

            var reconciled = await ReconcileAsync(operation, current, provider, ct);
            if (!reconciled.TryGetValue(out var outcome))
                return new PaymentOperationError.ProviderUnavailable();

            var canonical = outcome.Attempt;
            if (current.State.IsTerminal())
            {
                PaymentOperationState? providerState = null;
                if (outcome.Evaluation.TryGetValue(out var duplicate))
                    providerState = duplicate.State;
                else if (outcome.Evaluation.TryGetError(out var rejection)
                    && rejection.Reason == PaymentOperationTransitionRejectionReason.TerminalStateProtected)
                    providerState = rejection.ObservedState;

                if (providerState is null)
                    return new PaymentOperationError.ProviderUnavailable();
                if (!IsRetryCompatibleProviderTruth(current, providerState.Value, provider))
                    return new PaymentOperationError.OperationConflict();
            }
            else if (!outcome.Evaluation.TryGetValue(out _))
                return new PaymentOperationError.ProviderUnavailable();

            var retry = PaymentOperationRetryEvaluator.Evaluate(
                canonical.ToProviderAttempt(operation.SessionKind, operation.RequestFingerprint),
                new(
                    PaymentOperationRetryTrigger.ExplicitConsumerRetry,
                    operation.RequestFingerprint,
                    Guid.CreateVersion7(timeProvider.GetUtcNow())));
            if (!retry.TryGetValue(out var decision)
                || decision.Disposition != PaymentOperationRetryDisposition.CreateNewAttempt)
            {
                return new PaymentOperationError.OperationConflict();
            }

            if (provider.CanCancel)
            {
                var canceled = await stripeSessionClient.CancelAsync(
                    current.ProviderObjectKind,
                    current.ProviderObjectId,
                    ct);
                if (!canceled.TryGetValue(out _))
                {
                    retrieved = await stripeSessionClient.RetrieveAsync(
                        current.ProviderObjectKind,
                        current.ProviderObjectId,
                        ct);
                    if (!retrieved.TryGetValue(out provider)
                        || !string.Equals(provider.Status, "canceled", StringComparison.Ordinal))
                    {
                        return new PaymentOperationError.ProviderUnavailable();
                    }
                }
            }
            else if (!string.Equals(provider.Status, "canceled", StringComparison.Ordinal))
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
        PaymentSessionStatusRequest request,
        CancellationToken ct = default) =>
        await RefreshAsync(request.OperationId, request.OwnerId, ct);

    internal Task<Result<PaymentSessionStatus, PaymentOperationError>> RefreshAsync(
        Guid operationId,
        CancellationToken ct = default) =>
        RefreshAsync(operationId, null, ct);

    private async Task<Result<PaymentSessionStatus, PaymentOperationError>> RefreshAsync(
        Guid operationId,
        Guid? ownerId,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetByOperationIdAsync(operationId, ct);
        if (operation is null)
            return new PaymentOperationError.Unknown();
        if (ownerId is { } scope && !Owns(operation, scope))
            return new PaymentOperationError.Unknown();

        var attempt = operation.CurrentAttempt;
        if (attempt.ProviderObjectId is null)
            return ToStatus(attempt);

        var retrieved = await stripeSessionClient.RetrieveAsync(
            attempt.ProviderObjectKind,
            attempt.ProviderObjectId,
            ct);
        if (!retrieved.TryGetValue(out var provider))
        {
            await ReconcileAsync(operation, attempt, null, ct);
            return new PaymentOperationError.ProviderUnavailable();
        }

        var reconciled = await ReconcileAsync(operation, attempt, provider, ct);
        if (!reconciled.TryGetValue(out var outcome)
            || !outcome.Evaluation.TryGetValue(out _))
        {
            return new PaymentOperationError.ProviderUnavailable();
        }

        return ToStatus(outcome.Attempt);
    }

    private async Task<Result<PaymentSessionExecution, PaymentOperationError>> ExecuteAsync(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        CancellationToken ct)
    {
        Result<ProviderSession, PaymentOperationError.ProviderUnavailable> providerResult;
        if (attempt.ProviderObjectId is null)
        {
            var request = PaymentSessionProviderRequest.Create(operation, attempt);
            providerResult = await stripeSessionClient.CreateAsync(
                request,
                StripeIdempotencyKey.ForSessionAttempt(
                    operation.OperationId,
                    attempt.AttemptId,
                    attempt.Revision),
                ct);
        }
        else
        {
            providerResult = await stripeSessionClient.RetrieveAsync(
                attempt.ProviderObjectKind,
                attempt.ProviderObjectId,
                ct);
        }

        if (!providerResult.TryGetValue(out var provider))
        {
            await ReconcileAsync(operation, attempt, null, ct);
            return new PaymentOperationError.ProviderUnavailable();
        }

        var reconciled = await ReconcileAsync(operation, attempt, provider, ct);
        if (!reconciled.TryGetValue(out var outcome)
            || !outcome.Evaluation.TryGetValue(out _))
        {
            return new PaymentOperationError.ProviderUnavailable();
        }

        var customerSession = await stripeSessionClient.CreateCustomerSessionAsync(
            operation.ProviderCustomerId,
            ct);
        if (!customerSession.TryGetValue(out var customerSessionSecret))
            return new PaymentOperationError.ProviderUnavailable();

        return new PaymentSessionExecution(
            new(operation.OperationId, outcome.Attempt.AttemptId, outcome.Attempt.Revision),
            operation.SessionKind,
            outcome.Attempt.State,
            provider.ClientSecret,
            customerSessionSecret,
            operation.ProviderCustomerId);
    }

    private Task<Result<PaymentSessionReconciliation, PaymentOperationError.ProviderUnavailable>> ReconcileAsync(
        PaymentSessionOperationEntity operation,
        PaymentSessionAttemptEntity attempt,
        ProviderSession? provider,
        CancellationToken ct = default) =>
        reconciliationService.ReconcileAsync(
            new(
                operation,
                attempt,
                PaymentSessionReconciliationSource.Eager,
                provider),
            ct);

    private bool IsRetryCompatibleProviderTruth(
        PaymentSessionAttemptEntity current,
        PaymentOperationState providerState,
        ProviderSession provider) =>
        (current.State, current.FailureCode, providerState, provider.FailureClassification) switch
        {
            (_, _, PaymentOperationState.Canceled, _) => true,
            (PaymentOperationState.Failed, _, PaymentOperationState.Failed, _) => true,
            (
                PaymentOperationState.Failed,
                PaymentOperationFailureCode.Declined,
                PaymentOperationState.RequiresPaymentMethod,
                ProviderFailureClassification.Declined) => true,
            (
                PaymentOperationState.Canceled,
                PaymentOperationFailureCode.Expired,
                PaymentOperationState.Authorized,
                _) => provider.CaptureBefore <= timeProvider.GetUtcNow(),
            _ => false
        };

    private static PaymentSessionStatus ToStatus(PaymentSessionAttemptEntity attempt) =>
        new(
            new(attempt.OperationId, attempt.AttemptId, attempt.Revision),
            attempt.State,
            attempt.State.ToTerminalDisposition(false),
            attempt.FailureCode == PaymentOperationFailureCode.Expired
                ? PaymentOperationRetryDisposition.CreateNewAttempt
                : attempt.State.ToRetryDisposition(),
            attempt.ExpiresAt,
            attempt.CaptureBefore,
            attempt.FailureCode is { } code ? PaymentOperationFailure.FromCode(code) : null);

    private static bool Owns(PaymentSessionOperationEntity operation, Guid ownerId)
    {
        var ownerKey = ownerId.ToString("D");
        return IsPayer(operation, ownerId)
            || string.Equals(operation.PayeeOwnerKey, ownerKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPayer(PaymentSessionOperationEntity operation, Guid ownerId) =>
        string.Equals(
            operation.PayerOwnerKey,
            ownerId.ToString("D"),
            StringComparison.OrdinalIgnoreCase);

    private static bool TryValidate(
        PaymentOperationReference candidate,
        out PaymentOperationReference reference)
    {
        try
        {
            reference = candidate.EnsureValid();
            return true;
        }
        catch (ArgumentException)
        {
            reference = default;
            return false;
        }
    }
}
