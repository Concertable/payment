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

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoEscrowResponse();
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

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoEscrowResponse();
    }

    public override async Task<ReleaseByBookingIdResponse> ReleaseByBookingId(ReleaseByBookingIdRequest request, ServerCallContext context)
    {
        var result = await escrowService.ReleaseByBookingIdAsync(request.BookingId, context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return new ReleaseByBookingIdResponse
        {
            Transfer = result.Value is not null
                ? new TransferResponse { TransferId = result.Value.TransferId }
                : null
        };
    }

    public override async Task<RefundByBookingIdResponse> RefundByBookingId(RefundByBookingIdRequest request, ServerCallContext context)
    {
        var result = await escrowService.RefundByBookingIdAsync(request.BookingId, ct: context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return new RefundByBookingIdResponse
        {
            Refund = result.Value is not null
                ? new RefundResponse { RefundId = result.Value.RefundId }
                : null
        };
    }
}
