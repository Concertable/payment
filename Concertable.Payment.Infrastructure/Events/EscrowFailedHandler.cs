using Concertable.Payment.Infrastructure;
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
            logger.NoEscrowFoundForPaymentFailed(@event.TransactionId);
            return;
        }

        if (escrow.Status != EscrowStatus.Pending)
        {
            logger.EscrowAlreadyFailedStatus(escrow.Id, escrow.Status);
            return;
        }

        escrow.Fail();
        await escrowRepository.SaveChangesAsync();

        logger.EscrowFailed(escrow.Id, escrow.ChargeId, @event.FailureCode, @event.FailureMessage);
    }
}
