using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class EscrowMappers
{
    extension(EscrowDeposit deposit)
    {
        public EscrowResponse ToProtoEscrowResponse()
        {
            var message = new EscrowResponse
            {
                EscrowId = deposit.EscrowId,
                Status = deposit.Status.ToProtoStatus()
            };
            if (deposit.ClientSecret is { } clientSecret)
                message.ClientSecret = clientSecret;

            return message;
        }
    }

    extension(EscrowStatus status)
    {
        public EscrowStatusType ToProtoStatus() => status switch
        {
            EscrowStatus.Held => EscrowStatusType.EscrowHeld,
            EscrowStatus.Released => EscrowStatusType.EscrowReleased,
            EscrowStatus.Refunded => EscrowStatusType.EscrowRefunded,
            EscrowStatus.Disputed => EscrowStatusType.EscrowDisputed,
            EscrowStatus.Failed => EscrowStatusType.EscrowFailed,
            _ => EscrowStatusType.EscrowPending
        };
    }
}
