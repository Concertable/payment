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

    public async Task HandleAsync(PaymentSucceededEvent @event, CancellationToken ct)
    {
        var meta = @event.Metadata;

        await transactionService.LogAsync(new VerifyTransactionDto
        {
            ApplicationId = int.Parse(meta["applicationId"]),
            PayerId = Guid.Parse(meta["venueManagerId"]),
            PayeeId = Guid.Empty,
            PaymentIntentId = @event.TransactionId,
            Amount = 100,
            Status = TransactionStatus.Complete,
            CreatedAt = timeProvider.GetUtcNow().DateTime
        });
    }
}
