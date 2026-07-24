using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowService
{
    Task<Result<EscrowDeposit>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        Money amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<Transfer>> ReleaseAsync(int escrowId, CancellationToken ct = default);

    Task<Result<Transfer?>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Refund?>> RefundByBookingIdAsync(
        int bookingId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Refund>> RefundAsync(
        int escrowId,
        Money? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<EscrowDto?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
}
