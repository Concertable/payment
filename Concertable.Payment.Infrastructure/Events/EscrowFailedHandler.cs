using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class EscrowFailedHandler : IPaymentFailureHandler
{
    private readonly IEscrowRepository escrowRepository;
    private readonly ILogger<EscrowFailedHandler> logger;

    public EscrowFailedHandler(IEscrowRepository escrowRepository, ILogger<EscrowFailedHandler> logger)
    {
        this.escrowRepository = escrowRepository;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByChargeIdAsync(@event.TransactionId, ct);
        if (escrow is null)
        {
            logger.LogWarning(
                "No escrow found for charge {ChargeId}; ignoring PaymentFailedEvent",
                @event.TransactionId);
            return;
        }

        if (escrow.Status != EscrowStatus.Pending)
        {
            logger.LogInformation(
                "Escrow {EscrowId} already in status {Status}; skipping fail",
                escrow.Id, escrow.Status);
            return;
        }

        escrow.Fail();
        await escrowRepository.SaveChangesAsync();

        logger.LogInformation(
            "Escrow {EscrowId} failed (Pending -> Failed) for charge {ChargeId}: {Code} {Message}",
            escrow.Id, escrow.ChargeId, @event.FailureCode, @event.FailureMessage);
    }
}
