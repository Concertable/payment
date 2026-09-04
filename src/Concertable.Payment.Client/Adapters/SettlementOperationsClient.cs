using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Reunion;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class SettlementOperationsClient : ISettlementOperationsClient
{
    private readonly Proto.SettlementOperations.SettlementOperationsClient client;

    public SettlementOperationsClient(Proto.SettlementOperations.SettlementOperationsClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayAsync(
                Proto.SettlementPaymentRequest.Create(
                    operationId,
                    reference,
                    payerId,
                    payeeId,
                    amount,
                    paymentMethod,
                    session),
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToPaymentMethodChargeError(),
            ct);

    public Task<Result<PaymentOutcome, PaymentMethodChargeError>> PayBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.PayBoundCommissionAsync(
                Proto.BoundCommissionSettlementPaymentRequest.Create(
                    reference,
                    payerId,
                    payeeId,
                    gross,
                    paymentMethod,
                    session,
                    commissionBindingId,
                    externalReference),
                cancellationToken: ct)).ToPaymentOutcome(),
            error => error.ToPaymentMethodChargeError(),
            ct);

    public Task<Result<Option<Refund>, SettlementRefundError>> RefundBoundCommissionAsync(
        PaymentOperationReference reference,
        Money gross,
        string? reason = null,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync<Option<Refund>, SettlementRefundError>(
            async () =>
            {
                var response = await client.RefundBoundCommissionAsync(
                    Proto.BoundCommissionRefundRequest.Create(reference, gross, reason),
                    cancellationToken: ct);
                return string.IsNullOrEmpty(response.RefundId)
                    ? Option.None<Refund>()
                    : new Refund(response.RefundId);
            },
            error => error.ToSettlementRefundError(),
            ct);
}
