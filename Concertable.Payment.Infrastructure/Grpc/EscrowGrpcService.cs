using System.Globalization;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Domain;
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
        var result = await escrowService.DepositAsync(
            Guid.Parse(request.PayerId),
            Guid.Parse(request.PayeeId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.PaymentMethodId,
            request.Session.ToPaymentSession(),
            request.BookingId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return result.Value.ToProtoEscrowResponse();
    }

    public override async Task<EscrowResponse> Capture(CaptureRequest request, ServerCallContext context)
    {
        var result = await escrowService.CaptureAsync(
            Guid.Parse(request.PayerId),
            Guid.Parse(request.PayeeId),
            decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            request.PaymentIntentId,
            request.BookingId,
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
}
