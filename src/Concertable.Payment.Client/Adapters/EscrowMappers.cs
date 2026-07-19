using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class EscrowMappers
{
    public static EscrowDeposit ToEscrowDeposit(this Proto.EscrowResponse r) =>
        new(
            r.EscrowId,
            r.ChargeId,
            r.Status.ToEscrowStatus(),
            string.IsNullOrEmpty(r.ClientSecret) ? null : r.ClientSecret);

    public static EscrowStatus ToEscrowStatus(this Proto.EscrowStatusType status) => status switch
    {
        Proto.EscrowStatusType.EscrowPending => EscrowStatus.Pending,
        Proto.EscrowStatusType.EscrowHeld => EscrowStatus.Held,
        Proto.EscrowStatusType.EscrowReleased => EscrowStatus.Released,
        Proto.EscrowStatusType.EscrowRefunded => EscrowStatus.Refunded,
        Proto.EscrowStatusType.EscrowDisputed => EscrowStatus.Disputed,
        Proto.EscrowStatusType.EscrowFailed => EscrowStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
