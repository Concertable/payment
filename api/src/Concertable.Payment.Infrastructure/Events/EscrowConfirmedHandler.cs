using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class EscrowConfirmedHandler : ITransactionHandler
{
    private readonly IEscrowRepository escrowRepository;
    private readonly ILedgerService ledger;
    private readonly IUnitOfWork unitOfWork;
    private readonly ILogger<EscrowConfirmedHandler> logger;

    public EscrowConfirmedHandler(
        IEscrowRepository escrowRepository,
        ILedgerService ledger,
        IUnitOfWork unitOfWork,
        ILogger<EscrowConfirmedHandler> logger)
    {
        this.escrowRepository = escrowRepository;
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.logger = logger;
    }

    public async Task HandleAsync(
        PaymentSucceededEvent @event,
        string providerObjectId,
        CancellationToken ct)
    {
        var escrow = await escrowRepository.GetByChargeIdAsync(providerObjectId, ct);
        if (escrow is null)
        {
            logger.NoEscrowFoundForPaymentSucceeded(providerObjectId);
            return;
        }

        if (escrow.Status != EscrowStatus.Pending)
        {
            logger.EscrowAlreadyConfirmedStatus(escrow.Id, escrow.Status);
            return;
        }

        escrow.Confirm();
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                Money.FromMinorUnits(escrow.PayerTotalMinor, escrow.Currency),
                new PaymentOperationReference(escrow.OperationType, escrow.ClientReference),
                escrow.ChargeId),
            ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.EscrowConfirmed(escrow.Id, escrow.ChargeId);
    }
}
