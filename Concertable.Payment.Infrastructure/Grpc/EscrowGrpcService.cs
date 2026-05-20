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
            request.Session == PaymentSessionType.OffSession ? PaymentSession.OffSession : PaymentSession.OnSession,
            request.BookingId,
            context.CancellationToken);

        if (result.IsFailed)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Errors[0].Message));

        return MapEscrowResponse(result.Value);
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

        return MapEscrowResponse(result.Value);
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

    private static EscrowResponse MapEscrowResponse(Application.DTOs.EscrowResponse r) =>
        new()
        {
            EscrowId = r.EscrowId,
            ChargeId = r.ChargeId,
            Status = MapStatus(r.Status),
            ClientSecret = r.ClientSecret ?? ""
        };

    private static EscrowStatusType MapStatus(EscrowStatus s) => s switch
    {
        EscrowStatus.Held => EscrowStatusType.EscrowHeld,
        EscrowStatus.Released => EscrowStatusType.EscrowReleased,
        EscrowStatus.Refunded => EscrowStatusType.EscrowRefunded,
        EscrowStatus.Disputed => EscrowStatusType.EscrowDisputed,
        EscrowStatus.Failed => EscrowStatusType.EscrowFailed,
        _ => EscrowStatusType.EscrowPending
    };
}
