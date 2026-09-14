using Concertable.Payment.Application.DTOs;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class PaymentTransactionRecorder : ITransactionHandler
{
    private readonly ITransactionService transactionService;
    private readonly TimeProvider timeProvider;

    public PaymentTransactionRecorder(ITransactionService transactionService, TimeProvider timeProvider)
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

        await transactionService.LogAsync(new PaymentTransactionDto
        {
            OperationType = meta[PaymentMetadataKeys.OperationType],
            ClientReference = meta[PaymentMetadataKeys.ClientReference],
            PayerId = Guid.Parse(meta[PaymentMetadataKeys.PayerOwnerId]),
            PayeeId = Guid.Parse(meta[PaymentMetadataKeys.PayeeOwnerId]),
            PaymentIntentId = providerObjectId,
            Amount = long.TryParse(meta.GetValueOrDefault(PaymentMetadataKeys.AmountMinor), out var a) ? a : 0,
            Status = TransactionStatus.Complete,
            CreatedAt = timeProvider.GetUtcNow()
        });
    }
}
