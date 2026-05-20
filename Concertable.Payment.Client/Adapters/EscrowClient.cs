using System.Globalization;
using Concertable.Payment.Client;
using Concertable.Payment.Domain;
using FluentResults;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal sealed class EscrowClient : IEscrowClient
{
    private readonly Proto.Escrow.EscrowClient client;

    public EscrowClient(Proto.Escrow.EscrowClient client)
    {
        this.client = client;
    }

    public async Task<Result<EscrowResponse>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.DepositRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                PaymentMethodId = paymentMethodId,
                Session = MapSession(session),
                BookingId = bookingId
            };
            var response = await this.client.DepositAsync(request, cancellationToken: ct);
            return Result.Ok(MapEscrowResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<EscrowResponse>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.CaptureRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                PaymentIntentId = paymentIntentId,
                BookingId = bookingId
            };
            var response = await this.client.CaptureAsync(request, cancellationToken: ct);
            return Result.Ok(MapEscrowResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<TransferResponse?>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.ReleaseByBookingIdRequest { BookingId = bookingId };
            var response = await this.client.ReleaseByBookingIdAsync(request, cancellationToken: ct);
            TransferResponse? transfer = string.IsNullOrEmpty(response.Transfer?.TransferId)
                ? null
                : new TransferResponse(response.Transfer.TransferId);
            return Result.Ok(transfer);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    private static EscrowResponse MapEscrowResponse(Proto.EscrowResponse r) =>
        new(
            r.EscrowId,
            r.ChargeId,
            MapStatus(r.Status),
            string.IsNullOrEmpty(r.ClientSecret) ? null : r.ClientSecret);

    private static EscrowStatus MapStatus(Proto.EscrowStatusType status) => status switch
    {
        Proto.EscrowStatusType.EscrowHeld => EscrowStatus.Held,
        Proto.EscrowStatusType.EscrowReleased => EscrowStatus.Released,
        Proto.EscrowStatusType.EscrowRefunded => EscrowStatus.Refunded,
        Proto.EscrowStatusType.EscrowDisputed => EscrowStatus.Disputed,
        Proto.EscrowStatusType.EscrowFailed => EscrowStatus.Failed,
        _ => EscrowStatus.Pending
    };

    private static Proto.PaymentSessionType MapSession(PaymentSession session) =>
        session == PaymentSession.OffSession
            ? Proto.PaymentSessionType.OffSession
            : Proto.PaymentSessionType.OnSession;
}
