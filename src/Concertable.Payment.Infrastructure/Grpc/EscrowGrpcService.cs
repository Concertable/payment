using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Grpc;
using Grpc.Core;

namespace Concertable.Payment.Infrastructure.Grpc;

internal sealed class EscrowGrpcService : Escrow.EscrowBase
{
    private readonly IEscrowService escrowService;

    public EscrowGrpcService(IEscrowService escrowService)
    {
        this.escrowService = escrowService;
    }

    public override async Task<EscrowResponse> Deposit(DepositRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await escrowService.DepositAsync(
            command.PayerId,
            command.PayeeId,
            command.Amount,
            command.PaymentMethodId,
            command.Session,
            command.BookingId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> DepositBoundCommission(
        BoundCommissionDepositRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await escrowService.DepositBoundCommissionAsync(
            command.PayerId,
            command.PayeeId,
            command.Gross,
            command.PaymentMethodId,
            command.Session,
            command.BookingId,
            command.CommissionBindingId,
            command.ExternalReference,
            command.StripeSetupIntentId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> Capture(CaptureRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await escrowService.CaptureAsync(
            command.PayerId,
            command.PayeeId,
            command.Amount,
            command.PaymentIntentId,
            command.BookingId,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> CaptureBoundCommission(
        BoundCommissionCaptureRequest request,
        ServerCallContext context)
    {
        var command = request.ToCommand();
        var result = await escrowService.CaptureBoundCommissionAsync(
            command.PayerId,
            command.PayeeId,
            command.Gross,
            command.PaymentIntentId,
            command.BookingId,
            command.CommissionBindingId,
            command.ExternalReference,
            context.CancellationToken);

        return result.ValueOrRpcException().ToProtoEscrowResponse();
    }

    public override async Task<ReleaseByBookingIdResponse> ReleaseByBookingId(
        ReleaseByBookingIdRequest request,
        ServerCallContext context)
    {
        var result = await escrowService.ReleaseByBookingIdAsync(request.BookingId, context.CancellationToken);

        var transfer = result.ValueOrRpcException();
        return new ReleaseByBookingIdResponse
        {
            Transfer = transfer.Match<TransferResponse?>(
                value => new TransferResponse { TransferId = value.TransferId },
                () => null)
        };
    }

    public override async Task<RefundByBookingIdResponse> RefundByBookingId(
        RefundByBookingIdRequest request,
        ServerCallContext context)
    {
        var result = await escrowService.RefundByBookingIdAsync(request.BookingId, ct: context.CancellationToken);

        var refund = result.ValueOrRpcException();
        return new RefundByBookingIdResponse
        {
            Refund = refund.Match<RefundResponse?>(
                value => new RefundResponse { RefundId = value.RefundId },
                () => null)
        };
    }

    public override async Task<RefundByBookingIdResponse> RefundBoundCommissionByBookingId(
        BoundCommissionRefundByBookingIdRequest request,
        ServerCallContext context)
    {
        var result = await escrowService.RefundBoundCommissionByBookingIdAsync(
            request.BookingId,
            request.Gross.ToMoney(),
            ct: context.CancellationToken);

        var refund = result.ValueOrRpcException();
        return new RefundByBookingIdResponse
        {
            Refund = refund.Match<RefundResponse?>(
                value => new RefundResponse { RefundId = value.RefundId },
                () => null)
        };
    }

    private static RefundByBookingIdResponse ToResponse(Option<Refund> refund) =>
        new()
        {
            Refund = refund.Match<RefundResponse?>(
                value => new RefundResponse { RefundId = value.RefundId },
                () => null)
        };
}
