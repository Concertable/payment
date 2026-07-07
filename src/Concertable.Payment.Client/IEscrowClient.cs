using Concertable.Payment.Contracts;
using FluentResults;

namespace Concertable.Payment.Client;

public interface IEscrowClient
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

    Task<Result<Transfer?>> ReleaseByBookingIdAsync(int bookingId, CancellationToken ct = default);

    Task<Result<Refund?>> RefundByBookingIdAsync(int bookingId, CancellationToken ct = default);
}
