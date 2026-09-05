using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class EscrowMappers
{
    extension(Proto.EscrowResponse response)
    {
        public EscrowDeposit ToEscrowDeposit() => new(
            response.EscrowId,
            response.Status.ToEscrowStatus(),
            response.HasClientSecret ? response.ClientSecret : null);
    }

    extension(Proto.EscrowStatusType status)
    {
        public EscrowStatus ToEscrowStatus() => status switch
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
}
