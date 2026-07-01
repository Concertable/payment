using Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class EscrowMappers
{
    public static EscrowResponse ToProtoEscrowResponse(this Application.DTOs.EscrowDeposit r) =>
        new()
        {
            EscrowId = r.EscrowId,
            ChargeId = r.ChargeId,
            Status = r.Status.ToProtoStatus(),
            ClientSecret = r.ClientSecret ?? ""
        };

    public static EscrowStatusType ToProtoStatus(this EscrowStatus s) => s switch
    {
        EscrowStatus.Held => EscrowStatusType.EscrowHeld,
        EscrowStatus.Released => EscrowStatusType.EscrowReleased,
        EscrowStatus.Refunded => EscrowStatusType.EscrowRefunded,
        EscrowStatus.Disputed => EscrowStatusType.EscrowDisputed,
        EscrowStatus.Failed => EscrowStatusType.EscrowFailed,
        _ => EscrowStatusType.EscrowPending
    };
}
