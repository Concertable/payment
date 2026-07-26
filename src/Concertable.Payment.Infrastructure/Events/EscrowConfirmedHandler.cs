using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class EscrowConfirmedHandler : ITransactionHandler
{
    private readonly IEscrowRepository escrowRepository;
    private readonly ILedgerService ledger;
    private readonly ILogger<EscrowConfirmedHandler> logger;

    public EscrowConfirmedHandler(IEscrowRepository escrowRepository, ILedgerService ledger, ILogger<EscrowConfirmedHandler> logger)
    {
        this.escrowRepository = escrowRepository;
        this.ledger = ledger;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent @event, CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByChargeIdAsync(@event.TransactionId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForPaymentSucceeded(@event.TransactionId);
            return;
        }

        if (escrow.Status != EscrowStatus.Pending)
        {
            logger.EscrowAlreadyConfirmedStatus(escrow.Id, escrow.Status);
            return;
        }

        escrow.Confirm();
        await ledger.PostAsync(
            LedgerPostings.EscrowHold(escrow.FromOwnerId, escrow.Amount, escrow.BookingId, escrow.ChargeId),
            ct);

        logger.EscrowConfirmed(escrow.Id, escrow.ChargeId);
    }
}
