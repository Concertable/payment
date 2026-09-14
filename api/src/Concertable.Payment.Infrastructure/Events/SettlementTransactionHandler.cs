namespace Concertable.Payment.Infrastructure.Events;

internal sealed class SettlementTransactionHandler : ITransactionHandler
{
    private readonly ITransactionService transactionService;

    public SettlementTransactionHandler(ITransactionService transactionService)
    {
        this.transactionService = transactionService;
    }

    public Task HandleAsync(
        PaymentSucceededEvent @event,
        string providerObjectId,
        CancellationToken ct) =>
        transactionService.CompleteAsync(providerObjectId, ct);
}
