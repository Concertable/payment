using Concertable.Payment.Application.DTOs;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class VerifyTransactionHandler : ITransactionHandler
{
    private readonly ITransactionService transactionService;
    private readonly TimeProvider timeProvider;

    public VerifyTransactionHandler(ITransactionService transactionService, TimeProvider timeProvider)
    {
        this.transactionService = transactionService;
        this.timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        PaymentSucceededEvent @event,
        string providerObjectId,
        CancellationToken ct)
    {
        var meta = @event.Metadata;

        await transactionService.LogAsync(new VerifyTransactionDto
        {
            OperationType = meta[PaymentMetadataKeys.OperationType],
            ClientReference = meta[PaymentMetadataKeys.ClientReference],
            PayerId = Guid.Parse(meta[PaymentMetadataKeys.PayerOwnerId]),
            PayeeId = Guid.Empty,
            PaymentIntentId = providerObjectId,
            Amount = 100,
            Status = TransactionStatus.Complete,
            CreatedAt = timeProvider.GetUtcNow()
        });
    }
}
