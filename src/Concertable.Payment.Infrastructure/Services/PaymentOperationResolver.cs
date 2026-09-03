using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Entities;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class PaymentOperationResolver : IPaymentOperationResolver
{
    private readonly IPaymentSessionOperationRepository operationRepository;

    public PaymentOperationResolver(IPaymentSessionOperationRepository operationRepository)
    {
        this.operationRepository = operationRepository;
    }

    public async Task<Result<string, PaymentOperationError>> ResolvePaymentMethodAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default)
    {
        var operation = await GetOwnedAsync(reference, payerOwnerId, ct);
        if (operation is null
            || operation.SessionKind is not (PaymentSessionKind.PaymentMethodSetup
                or PaymentSessionKind.PaymentMethodVerification)
            || operation.CurrentAttempt is not
            {
                State: PaymentOperationState.Succeeded,
                PaymentMethodId: { Length: > 0 } paymentMethodId
            })
        {
            return new PaymentOperationError.PaymentMethodRequired();
        }

        return paymentMethodId;
    }

    public async Task<Result<string, PaymentOperationError>> ResolveAuthorizationAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct = default)
    {
        var operation = await GetOwnedAsync(reference, payerOwnerId, ct);
        if (operation is null
            || operation.SessionKind != PaymentSessionKind.Authorization
            || operation.CurrentAttempt is not
            {
                State: PaymentOperationState.Authorized,
                ProviderObjectId: { Length: > 0 } providerObjectId
            })
        {
            return new PaymentOperationError.OperationConflict();
        }

        return providerObjectId;
    }

    private async Task<PaymentSessionOperationEntity?> GetOwnedAsync(
        PaymentOperationReference reference,
        Guid payerOwnerId,
        CancellationToken ct)
    {
        var operation = await operationRepository.GetByReferenceAsync(
            reference.OperationType,
            reference.ConsumerCorrelation,
            ct);
        return operation is not null
            && string.Equals(
                operation.PayerOwnerKey,
                payerOwnerId.ToString("D"),
                StringComparison.Ordinal)
            ? operation
            : null;
    }
}
