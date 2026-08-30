using Concertable.DataAccess.Application;
using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.Interfaces;

internal interface IEscrowRepository : IRepository<EscrowEntity>
{
    Task<EscrowEntity?> GetWithRefundsByIdAsync(int id, CancellationToken ct = default);
    Task<EscrowEntity?> GetByBookingIdAsync(int bookingId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByChargeIdAsync(string chargeId, CancellationToken ct = default);
    Task<EscrowEntity?> GetByCommissionBindingIdAsync(
        Guid commissionBindingId,
        CancellationToken ct = default);
    Task<(EscrowEntity? Escrow, bool Conflict)> ReserveReleaseAsync(
        int escrowId,
        Guid operationId,
        SettlementOperationFingerprint fingerprint,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically reserves <paramref name="grossMinor"/> against the escrow's cumulative gross-refund
    /// ceiling in a single conditional write. Returns <see langword="true"/> when the reservation fits
    /// within <c>PayeeGrossMinor</c> (and the escrow is refundable), <see langword="false"/> when a
    /// concurrent refund already consumed the remaining capacity — the lost-update-safe replacement for
    /// an optimistic-concurrency reservation.
    /// </summary>
    Task<bool> TryReserveRefundGrossAsync(int escrowId, long grossMinor, CancellationToken ct = default);

    /// <summary>Releases a previously-reserved gross amount after its Stripe refund fails.</summary>
    Task ReleaseReservedRefundGrossAsync(int escrowId, long grossMinor, CancellationToken ct = default);
}
