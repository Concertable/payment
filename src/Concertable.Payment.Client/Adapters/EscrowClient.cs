using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;
using Functional = Concertable.Kernel.Functional;

namespace Concertable.Payment.Client.Adapters;

internal sealed class EscrowClient : IEscrowClient
{
    private readonly Proto.Escrow.EscrowClient client;

    public EscrowClient(Proto.Escrow.EscrowClient client)
    {
        this.client = client;
    }

    public async Task<Functional.Result<EscrowDeposit, DepositError>> CreateDepositAsync(
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
            return Functional.Result.Success<EscrowDeposit, DepositError>(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<EscrowDeposit, DepositError>(ex.ToDepositError());
        }
    }

    public async Task<Functional.Result<EscrowDeposit, DepositError>> CreateBoundCommissionDepositAsync(
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
            return Functional.Result.Success<EscrowDeposit, DepositError>(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<EscrowDeposit, DepositError>(ex.ToDepositError());
        }
    }

    public async Task<Functional.Result<EscrowDeposit, CaptureError>> CaptureDepositAsync(
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
            return Functional.Result.Success<EscrowDeposit, CaptureError>(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<EscrowDeposit, CaptureError>(ex.ToCaptureError());
        }
    }

    public async Task<Functional.Result<EscrowDeposit, CaptureError>> CaptureBoundCommissionDepositAsync(
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
            return Functional.Result.Success<EscrowDeposit, CaptureError>(response.ToEscrowDeposit());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<EscrowDeposit, CaptureError>(ex.ToCaptureError());
        }
    }

    public async Task<Functional.Result<Functional.Option<Transfer>, ReleaseError>> ReleaseAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.ReleaseByBookingIdRequest { BookingId = bookingId };
            var response = await client.ReleaseByBookingIdAsync(request, cancellationToken: ct);
            var transfer = string.IsNullOrEmpty(response.Transfer?.TransferId)
                ? Functional.Option.None<Transfer>()
                : Functional.Option.Some(new Transfer(response.Transfer.TransferId));
            return Functional.Result.Success<Functional.Option<Transfer>, ReleaseError>(transfer);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<Functional.Option<Transfer>, ReleaseError>(ex.ToReleaseError());
        }
    }

    public async Task<Functional.Result<Functional.Option<Refund>, RefundError>> RefundAsync(
        int bookingId,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Proto.RefundByBookingIdRequest { BookingId = bookingId };
            var response = await client.RefundByBookingIdAsync(request, cancellationToken: ct);
            var refund = string.IsNullOrEmpty(response.Refund?.RefundId)
                ? Functional.Option.None<Refund>()
                : Functional.Option.Some(new Refund(response.Refund.RefundId));
            return Functional.Result.Success<Functional.Option<Refund>, RefundError>(refund);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<Functional.Option<Refund>, RefundError>(ex.ToRefundError());
        }
    }

    public async Task<Functional.Result<Functional.Option<Refund>, RefundError>> RefundBoundCommissionAsync(
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
            var refund = string.IsNullOrEmpty(response.Refund?.RefundId)
                ? Functional.Option.None<Refund>()
                : Functional.Option.Some(new Refund(response.Refund.RefundId));
            return Functional.Result.Success<Functional.Option<Refund>, RefundError>(refund);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            return Functional.Result.Failure<Functional.Option<Refund>, RefundError>(ex.ToRefundError());
        }
    }
}
