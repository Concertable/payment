using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Reunion;

namespace Concertable.Payment.Client;

public interface ISettlementOperationsClient
{
    Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        CancellationToken ct = default);

    Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, SettlementRefundError>> RefundBoundCommissionAsync(
        PaymentOperationReference reference,
        Money gross,
        string? reason = null,
        CancellationToken ct = default);
}
