using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using FluentResults;

namespace Concertable.Payment.Client;

public interface IEscrowClient
{
    Task<Result<EscrowDeposit>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit>> DepositBoundCommissionAsync(
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
        CancellationToken ct = default);

    Task<Result<EscrowDeposit>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit>> CaptureBoundCommissionAsync(
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
        CancellationToken ct = default);

    Task<Result<Transfer?>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Refund?>> RefundByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Refund?>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        CancellationToken ct = default);
}
