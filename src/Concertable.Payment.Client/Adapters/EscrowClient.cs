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
        Money amount,
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
                Amount = amount.ToProtoMoney(),
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

    public async Task<Result<EscrowDeposit>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        string? stripeSetupIntentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await client.DepositBoundCommissionAsync(
                new Proto.BoundCommissionDepositRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    PaymentMethodId = paymentMethodId,
                    Session = session.ToProtoSession(),
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    ExpectedCommissionMinor = expectedCommissionMinor,
                    ExpectedPayerTotalMinor = expectedPayerTotalMinor,
                    StripeSetupIntentId = stripeSetupIntentId ?? string.Empty
                },
                cancellationToken: ct);
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
        Money amount,
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
                Amount = amount.ToProtoMoney(),
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

    public async Task<Result<EscrowDeposit>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor,
        CancellationToken ct = default)
    {
        try
        {
            var response = await client.CaptureBoundCommissionAsync(
                new Proto.BoundCommissionCaptureRequest
                {
                    PayerId = payerId.ToString(),
                    PayeeId = payeeId.ToString(),
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency(),
                    PaymentIntentId = paymentIntentId,
                    BookingId = bookingId,
                    CommissionBindingId = commissionBindingId.ToString(),
                    ExternalReference = externalReference,
                    ExpectedCommissionMinor = expectedCommissionMinor,
                    ExpectedPayerTotalMinor = expectedPayerTotalMinor
                },
                cancellationToken: ct);
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

    public async Task<Result<Refund?>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        CancellationToken ct = default)
    {
        try
        {
            var response = await client.RefundBoundCommissionByBookingIdAsync(
                new Proto.BoundCommissionRefundByBookingIdRequest
                {
                    BookingId = bookingId,
                    GrossMinor = grossMinor,
                    Currency = currency.ToProtoCurrency()
                },
                cancellationToken: ct);
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
