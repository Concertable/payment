using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class EscrowConfirmedHandler : ITransactionHandler
{
    private readonly IEscrowRepository escrowRepository;
    private readonly ILogger<EscrowConfirmedHandler> logger;

    public EscrowConfirmedHandler(IEscrowRepository escrowRepository, ILogger<EscrowConfirmedHandler> logger)
    {
        this.escrowRepository = escrowRepository;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent @event, CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByChargeIdAsync(@event.TransactionId, ct);
        if (escrow is null)
        {
            logger.LogWarning(
                "No escrow found for charge {ChargeId}; ignoring PaymentSucceededEvent",
                @event.TransactionId);
            return;
        }

        if (escrow.Status != EscrowStatus.Pending)
        {
            logger.LogInformation(
                "Escrow {EscrowId} already in status {Status}; skipping confirm",
                escrow.Id, escrow.Status);
            return;
        }

        escrow.Confirm();
        await escrowRepository.SaveChangesAsync();

        logger.LogInformation(
            "Escrow {EscrowId} confirmed (Pending -> Held) for charge {ChargeId}",
            escrow.Id, escrow.ChargeId);
    }
}
