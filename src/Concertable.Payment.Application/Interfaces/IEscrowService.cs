using Concertable.Kernel.Functional;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowService
{
    Task<Result<EscrowDeposit, DepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, DepositError>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, CaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, CaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        long grossMinor,
        Currency currency,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<Transfer, ReleaseError>> ReleaseAsync(int escrowId, CancellationToken ct = default);

    Task<Result<Option<Transfer>, ReleaseError>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Option<Refund>, RefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, RefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        long grossMinor,
        Currency currency,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Refund, RefundError>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Option<EscrowDto>> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
}
