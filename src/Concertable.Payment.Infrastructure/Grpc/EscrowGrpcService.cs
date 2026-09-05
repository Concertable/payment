using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Grpc;
using Grpc.Core;
using Reunion;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class EscrowGrpcService : Escrow.EscrowBase
{
    private readonly IEscrowService escrowService;
    private readonly IPaymentOperationResolver paymentOperationResolver;

    public EscrowGrpcService(
        IEscrowService escrowService,
        IPaymentOperationResolver paymentOperationResolver)
    {
        this.escrowService = escrowService;
        this.paymentOperationResolver = paymentOperationResolver;
    }

    public override async Task<EscrowResponse> Deposit(DepositRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var paymentMethod = await paymentOperationResolver.ResolvePaymentMethodAsync(
            command.PaymentMethod,
            command.PayerId,
            context.CancellationToken);
        if (!paymentMethod.TryGetValue(out var paymentMethodId))
        {
            paymentMethod.TryGetError(out var error);
            throw new EscrowDepositError.PaymentOperationFailure(error!).ToRpcException();
        }

        var result = await escrowService.DepositAsync(
            command.PayerId,
            command.PayeeId,
            command.Amount,
            paymentMethodId,
            command.Session,
            command.Reference,
            command.OperationId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> DepositBoundCommission(
        BoundCommissionDepositRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var paymentMethod = await paymentOperationResolver.ResolvePaymentMethodAsync(
            command.PaymentMethod,
            command.PayerId,
            context.CancellationToken);
        if (!paymentMethod.TryGetValue(out var paymentMethodId))
        {
            paymentMethod.TryGetError(out var error);
            throw new EscrowDepositError.PaymentOperationFailure(error!).ToRpcException();
        }

        var result = await escrowService.DepositBoundCommissionAsync(
            command.PayerId,
            command.PayeeId,
            command.Gross,
            paymentMethodId,
            command.Session,
            command.Reference,
            command.CommissionBindingId,
            command.ExternalReference,
            null,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> Capture(CaptureRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var authorization = await paymentOperationResolver.ResolveAuthorizationAsync(
            command.Authorization,
            command.PayerId,
            context.CancellationToken);
        if (!authorization.TryGetValue(out var paymentIntentId))
        {
            authorization.TryGetError(out var error);
            throw new EscrowCaptureError.PaymentOperationFailure(error!).ToRpcException();
        }

        var result = await escrowService.CaptureAsync(
            command.PayerId,
            command.PayeeId,
            command.Amount,
            paymentIntentId,
            command.Reference,
            command.OperationId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> CaptureBoundCommission(
        BoundCommissionCaptureRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var authorization = await paymentOperationResolver.ResolveAuthorizationAsync(
            command.Authorization,
            command.PayerId,
            context.CancellationToken);
        if (!authorization.TryGetValue(out var paymentIntentId))
        {
            authorization.TryGetError(out var error);
            throw new EscrowCaptureError.PaymentOperationFailure(error!).ToRpcException();
        }

        var result = await escrowService.CaptureBoundCommissionAsync(
            command.PayerId,
            command.PayeeId,
            command.Gross,
            paymentIntentId,
            command.Reference,
            command.CommissionBindingId,
            command.ExternalReference,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<ReleaseEscrowResponse> Release(
        ReleaseEscrowRequest request,
        ServerCallContext context)
    {
        var transfer = (await escrowService.ReleaseByReferenceAsync(
            request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            request.Reference.ToContractReference(),
            context.CancellationToken)).ValueOrRpcException();

        return new ReleaseEscrowResponse
        {
            Transfer = transfer.Match<TransferResponse?>(
                value => new TransferResponse { OperationId = value.OperationId.ToString("D") },
                () => null)
        };
    }

    public override async Task<RefundEscrowResponse> Refund(
        RefundEscrowRequest request,
        ServerCallContext context)
    {
        var result = await escrowService.RefundByReferenceAsync(
            request.Reference.ToContractReference(),
            amount: null,
            reason: null,
            operationId: request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
            ct: context.CancellationToken);

        return ToResponse(result.ValueOrRpcException());
    }

    public override async Task<RefundEscrowResponse> RefundBoundCommission(
        BoundCommissionRefundRequest request,
        ServerCallContext context)
    {
        var result = await escrowService.RefundBoundCommissionByReferenceAsync(
            request.Reference.ToContractReference(),
            request.Gross.ToMoney(),
            ct: context.CancellationToken);

        return ToResponse(result.ValueOrRpcException());
    }

    private static RefundEscrowResponse ToResponse(Option<Refund> refund) =>
        new()
        {
            Refund = refund.Match<RefundResponse?>(
                value => new RefundResponse { Id = value.Id.ToString("D") },
                () => null)
        };
}
