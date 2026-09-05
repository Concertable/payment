using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Entities;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentOperationResolver : IPaymentOperationResolver
{
    private readonly IPaymentSessionOperationRepository operationRepository;
    private readonly IPaymentSessionReconciliationService reconciliationService;
    private readonly IStripeSessionClient stripeSessionClient;

    public PaymentOperationResolver(
        IPaymentSessionOperationRepository operationRepository,
        IPaymentSessionReconciliationService reconciliationService,
        IStripeSessionClient stripeSessionClient)
    {
        this.operationRepository = operationRepository;
        this.reconciliationService = reconciliationService;
        this.stripeSessionClient = stripeSessionClient;
    }

    public async Task<Result<string, PaymentOperationError>> ResolvePaymentMethodAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default)
    {
        var resolved = await ResolveCurrentAttemptAsync(
            reference,
            payerOwnerId,
            new PaymentOperationError.PaymentMethodRequired(),
            ct);
        return resolved.Match<Result<string, PaymentOperationError>>(
            current => ResolvePaymentMethod(current),
            error => error);
    }

    public async Task<Result<string, PaymentOperationError>> ResolveAuthorizationAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default)
    {
        var resolved = await ResolveCurrentAttemptAsync(
            reference,
            payerOwnerId,
            new PaymentOperationError.OperationConflict(),
            ct);
        return resolved.Match<Result<string, PaymentOperationError>>(
            current => ResolveAuthorization(current),
            error => error);
    }

    public async Task<string> ResolveProviderObjectIdAsync(
        PaymentOperationReference reference,
        CancellationToken ct = default)
    {
        var operation = await operationRepository.GetByReferenceAsync(reference, ct);
        return operation?.CurrentAttempt.ProviderObjectId
            ?? throw new InvalidOperationException(
                $"Payment operation {reference.OperationType}/{reference.ClientReference} has no provider transaction.");
    }

    private static Result<string, PaymentOperationError> ResolvePaymentMethod(
        ResolvedOperation current)
    {
        if (current.Operation.SessionKind is not (PaymentSessionKind.PaymentMethodSetup
                or PaymentSessionKind.PaymentMethodVerification)
            || current.Attempt is not
            {
                State: PaymentOperationState.Succeeded,
                PaymentMethodId: { Length: > 0 } paymentMethodId
            })
        {
            return FailureOr(
                current.Attempt,
                new PaymentOperationError.PaymentMethodRequired());
        }

        return paymentMethodId;
    }

    private static Result<string, PaymentOperationError> ResolveAuthorization(
        ResolvedOperation current)
    {
        if (current.Operation.SessionKind != PaymentSessionKind.Authorization
            || current.Attempt is not
            {
                State: PaymentOperationState.Authorized,
                ProviderObjectId: { Length: > 0 } providerObjectId
            })
        {
            return FailureOr(
                current.Attempt,
                new PaymentOperationError.OperationConflict());
        }

        return providerObjectId;
    }

    private async Task<Result<ResolvedOperation, PaymentOperationError>> ResolveCurrentAttemptAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        PaymentOperationError missing,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetByReferenceAsync(reference, ct);
        if (operation is null
            || !string.Equals(
                operation.PayerOwnerKey,
                payerOwnerId.ToString("D"),
                StringComparison.OrdinalIgnoreCase))
        {
            return missing;
        }

        var attempt = operation.CurrentAttempt;
        if (attempt.State is PaymentOperationState.Succeeded or PaymentOperationState.Authorized
            || attempt.TerminalAt is not null
            || attempt.ProviderObjectId is null)
        {
            return new ResolvedOperation(operation, attempt);
        }

        var retrieved = await stripeSessionClient.RetrieveAsync(
            attempt.ProviderObjectKind,
            attempt.ProviderObjectId,
            ct);
        if (!retrieved.TryGetValue(out var provider))
        {
            await reconciliationService.ReconcileAsync(
                new(
                    operation,
                    attempt,
                    PaymentSessionReconciliationSource.Eager,
                    null),
                ct);
            return new PaymentOperationError.ProviderUnavailable();
        }

        var reconciled = await reconciliationService.ReconcileAsync(
            new(
                operation,
                attempt,
                PaymentSessionReconciliationSource.Eager,
                provider),
            ct);
        if (!reconciled.TryGetValue(out var outcome)
            || !outcome.Evaluation.TryGetValue(out _))
        {
            return new PaymentOperationError.ProviderUnavailable();
        }

        return new ResolvedOperation(operation, outcome.Attempt);
    }

    private static PaymentOperationError FailureOr(
        PaymentSessionAttemptEntity attempt,
        PaymentOperationError fallback) =>
        attempt.FailureCode is { } failureCode
            ? PaymentOperationError.FromCode(failureCode)
            : fallback;

    private sealed record ResolvedOperation(
        PaymentSessionOperationEntity Operation,
        PaymentSessionAttemptEntity Attempt);
}
