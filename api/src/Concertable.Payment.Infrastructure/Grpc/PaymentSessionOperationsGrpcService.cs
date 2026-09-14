using Concertable.Payment.Application.Interfaces;
using Google.Protobuf.WellKnownTypes;
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

    public override async Task<Proto.PaymentMethodSetupResponse> SetupPaymentMethod(
        Proto.PaymentMethodSetupRequest request,
        ServerCallContext context) =>
        (await paymentSessionService.SetupPaymentMethodAsync(
            request.ToContract(),
            context.CancellationToken))
        .ValueOrRpcException()
        .ToPaymentMethodSetupProto();

    public override async Task<Empty> ValidatePaymentMethod(
        Proto.PaymentMethodValidationRequest request,
        ServerCallContext context)
    {
        (await paymentSessionService.ValidatePaymentMethodAsync(
            request.ToContract(),
            context.CancellationToken)).SuccessOrRpcException();
        return new Empty();
    }

    public override async Task<Proto.PaymentSessionDescriptor> Create(
        Proto.PaymentSessionOperationRequest request,
        ServerCallContext context) =>
        (await paymentSessionService.CreateAsync(
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
