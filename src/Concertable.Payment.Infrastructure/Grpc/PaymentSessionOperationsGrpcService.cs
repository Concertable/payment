using Concertable.Payment.Application.Interfaces;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class PaymentSessionOperationsGrpcService
    : Proto.PaymentSessionOperations.PaymentSessionOperationsBase
{
    private readonly IPaymentSessionService paymentSessionService;

    public PaymentSessionOperationsGrpcService(IPaymentSessionService paymentSessionService)
    {
        this.paymentSessionService = paymentSessionService;
    }

    public override async Task<Proto.PaymentSessionDescriptor> CreateOrReplay(
        Proto.PaymentSessionOperationRequest request,
        ServerCallContext context) =>
        (await paymentSessionService.CreateOrReplayAsync(
            request.ToContract(),
            context.CancellationToken))
        .ValueOrRpcException()
        .ToProto();

    public override async Task<Proto.PaymentSessionDescriptor> Retry(
        Proto.PaymentSessionRetryRequest request,
        ServerCallContext context) =>
        (await paymentSessionService.RetryAsync(
            request.ToContract(),
            context.CancellationToken))
        .ValueOrRpcException()
        .ToProto();

    public override async Task<Proto.PaymentOperationSnapshot> GetStatus(
        Proto.PaymentSessionStatusRequest request,
        ServerCallContext context) =>
        (await paymentSessionService.RefreshAsync(
            request.ToContract(),
            context.CancellationToken))
        .ValueOrRpcException()
        .ToProto();
}
