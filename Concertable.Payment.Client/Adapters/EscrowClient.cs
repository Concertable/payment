using System.Globalization;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
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
                Session = session.ToProtoSession(),
                BookingId = bookingId
            };
            var response = await client.DepositAsync(request, cancellationToken: ct);
            return Result.Ok(response.ToEscrowResponse());
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
            return Result.Ok(response.ToEscrowResponse());
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
            var response = await client.ReleaseByBookingIdAsync(request, cancellationToken: ct);
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

}
