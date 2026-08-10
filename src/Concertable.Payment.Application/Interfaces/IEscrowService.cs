using Reunion;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowService
{
    Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        string? stripeSetupIntentId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        Guid payerId,
        Guid payeeId,
        Money gross,
        string paymentIntentId,
        int bookingId,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<Transfer, EscrowReleaseError>> ReleaseAsync(int escrowId, CancellationToken ct = default);

    Task<Result<Option<Transfer>, EscrowReleaseError>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionByBookingIdAsync(
        int bookingId,
        Money gross,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Refund, EscrowRefundError>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Option<EscrowDto>> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
}
