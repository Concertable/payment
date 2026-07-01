using FluentResults;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowService
{
    Task<Result<EscrowDeposit>> DepositAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentMethodId,
        PaymentSession session,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit>> CaptureAsync(
        Guid payerId,
        Guid payeeId,
        decimal amount,
        string paymentIntentId,
        int bookingId,
        CancellationToken ct = default);

    Task<Result<Transfer>> ReleaseAsync(int escrowId, CancellationToken ct = default);

    Task<Result<Transfer?>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Refund?>> RefundByBookingIdAsync(
        int bookingId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<Result<Refund>> RefundAsync(
        int escrowId,
        decimal? amount = null,
        string? reason = null,
        CancellationToken ct = default);

    Task<EscrowDto?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
}
