using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class SettlementFailedHandler : IPaymentFailureHandler
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

    public async Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct)
    {
        var transaction = await transactionRepository.GetByPaymentIntentIdAsync(@event.TransactionId);
        if (transaction is null)
        {
            logger.LogWarning(
                "No settlement transaction found for charge {ChargeId}; ignoring PaymentFailedEvent",
                @event.TransactionId);
            return;
        }

        if (transaction.Status != TransactionStatus.Pending)
        {
            logger.LogInformation(
                "Settlement transaction {TransactionId} already in status {Status}; skipping fail",
                transaction.Id, transaction.Status);
            return;
        }

        transaction.Fail();
        await transactionRepository.SaveChangesAsync();

        logger.LogInformation(
            "Settlement transaction {TransactionId} failed (Pending -> Failed) for charge {ChargeId}: {Code} {Message}",
            transaction.Id, transaction.PaymentIntentId, @event.FailureCode, @event.FailureMessage);
    }
}
