using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class SettlementFailedHandler : IPaymentFailureHandler
{
    private readonly ITransactionRepository transactionRepository;
    private readonly ILogger<SettlementFailedHandler> logger;

    public SettlementFailedHandler(
        ITransactionRepository transactionRepository,
        ILogger<SettlementFailedHandler> logger)
    {
        this.transactionRepository = transactionRepository;
        this.logger = logger;
    }

    public async Task HandleAsync(
        PaymentFailedEvent @event,
        string providerObjectId,
        CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByPaymentIntentIdAsync(providerObjectId);
        if (transaction is null)
        {
            logger.NoSettlementTransactionFound(providerObjectId);
            return;
        }

        if (transaction.Status != TransactionStatus.Pending)
        {
            logger.SettlementTransactionAlreadyInStatus(transaction.Id, transaction.Status);
            return;
        }

        transaction.Fail();
        await transactionRepository.SaveChangesAsync();

        logger.SettlementTransactionFailed(transaction.Id, transaction.PaymentIntentId, @event.FailureCode, @event.FailureMessage);
    }
}
