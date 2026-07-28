using Concertable.Kernel.ValueObjects;
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

    public async Task<Result<EscrowDeposit>> DepositAsync(
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
            var money = Money.Gbp(amount);
            var request = new Proto.DepositRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = money.ToProtoMoney(),
                PaymentMethodId = paymentMethodId,
                Session = session.ToProtoSession(),
                BookingId = bookingId
            };
            var response = await client.DepositAsync(request, cancellationToken: ct);
            return Result.Ok(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<EscrowDeposit>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var money = Money.Gbp(amount);
            var request = new Proto.CaptureRequest
            {
                PayerId = payerId.ToString(),
                PayeeId = payeeId.ToString(),
                Amount = money.ToProtoMoney(),
                PaymentIntentId = paymentIntentId,
                BookingId = bookingId
            };
            var response = await this.client.CaptureAsync(request, cancellationToken: ct);
            return Result.Ok(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<Transfer?>> ReleaseByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.ReleaseByBookingIdRequest { BookingId = bookingId };
            var response = await client.ReleaseByBookingIdAsync(request, cancellationToken: ct);
            Transfer? transfer = string.IsNullOrEmpty(response.Transfer?.TransferId)
                ? null
                : new Transfer(response.Transfer.TransferId);
            return Result.Ok(transfer);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }

    public async Task<Result<Refund?>> RefundByBookingIdAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.RefundByBookingIdRequest { BookingId = bookingId };
            var response = await client.RefundByBookingIdAsync(request, cancellationToken: ct);
            Refund? refund = string.IsNullOrEmpty(response.Refund?.RefundId)
                ? null
                : new Refund(response.Refund.RefundId);
            return Result.Ok(refund);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Result.Fail(ex.Status.Detail);
        }
    }
}
