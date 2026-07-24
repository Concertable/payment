using Concertable.Payment.Application.DTOs;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class TicketTransactionHandler : ITransactionHandler
{
    private readonly ITransactionService transactionService;
    private readonly TimeProvider timeProvider;

    public TicketTransactionHandler(ITransactionService transactionService, TimeProvider timeProvider)
    {
        this.transactionService = transactionService;
        this.timeProvider = timeProvider;
    }

    public async Task HandleAsync(PaymentSucceededEvent @event, CancellationToken ct)
    {
        var meta = @event.Metadata;

        await transactionService.LogAsync(new TicketTransactionDto
        {
            ConcertId = int.Parse(meta[PaymentMetadataKeys.ConcertId]),
            PayerId = Guid.Parse(meta[PaymentMetadataKeys.FromUserId]),
            PayeeId = Guid.Parse(meta[PaymentMetadataKeys.ToUserId]),
            PaymentIntentId = @event.TransactionId,
            Amount = long.TryParse(meta.GetValueOrDefault(PaymentMetadataKeys.Amount), out var a) ? a : 0,
            Status = TransactionStatus.Complete,
            CreatedAt = timeProvider.GetUtcNow().DateTime
        });
    }
}
