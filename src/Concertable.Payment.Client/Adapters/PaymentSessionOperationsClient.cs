using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Reunion;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class PaymentSessionOperationsClient : IPaymentSessionOperationsClient
{
    private readonly Proto.PaymentSessionOperations.PaymentSessionOperationsClient client;

    public PaymentSessionOperationsClient(
        Proto.PaymentSessionOperations.PaymentSessionOperationsClient client)
    {
        this.client = client;
    }

    public Task<Result<PaymentMethodSetup, PaymentOperationError>> SetupPaymentMethodAsync(
        PaymentMethodSetupRequest request,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                var response = await client.SetupPaymentMethodAsync(
                    request.ToProto(),
                    cancellationToken: ct);
                return new PaymentMethodSetup(
                    response.ClientSecret,
                    string.IsNullOrEmpty(response.CustomerSessionSecret) ? null : response.CustomerSessionSecret,
                    string.IsNullOrEmpty(response.CustomerToken) ? null : response.CustomerToken);
            },
            error => error.ToPaymentOperationError(),
            ct);

    public Task<UnitResult<PaymentOperationError>> ValidatePaymentMethodAsync(
        PaymentMethodValidationRequest request,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () =>
            {
                await client.ValidatePaymentMethodAsync(
                    request.ToProto(),
                    cancellationToken: ct);
            },
            error => error.ToPaymentOperationError(),
            ct);

    public Task<Result<PaymentSessionDescriptor, PaymentOperationError>> CreateOrReplayAsync(
        PaymentSessionOperationRequest request,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.CreateOrReplayAsync(
                request.ToProto(),
                cancellationToken: ct)).ToPaymentSessionDescriptor(),
            error => error.ToPaymentOperationError(),
            ct);

    public Task<Result<PaymentSessionDescriptor, PaymentOperationError>> RetryAsync(
        PaymentSessionRetryRequest request,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.RetryAsync(
                request.ToProto(),
                cancellationToken: ct)).ToPaymentSessionDescriptor(),
            error => error.ToPaymentOperationError(),
            ct);

    public Task<Result<PaymentOperationSnapshot, PaymentOperationError>> GetStatusAsync(
        PaymentSessionStatusRequest request,
        CancellationToken ct = default) =>
        PaymentClientResults.ExecuteAsync(
            async () => (await client.GetStatusAsync(
                request.ToProto(),
                cancellationToken: ct)).ToPaymentOperationSnapshot(),
            error => error.ToPaymentOperationError(),
            ct);
}
