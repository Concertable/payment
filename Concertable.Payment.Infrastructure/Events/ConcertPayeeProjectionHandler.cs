using Concertable.B2B.Concert.Contracts.Events;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure.Repositories;

namespace Concertable.Payment.Infrastructure.Events;

internal class ConcertPayeeProjectionHandler : IIntegrationEventHandler<ConcertChangedEvent>
{
    private readonly IConcertPayeeRepository concertPayeeRepository;

    public ConcertPayeeProjectionHandler(IConcertPayeeRepository concertPayeeRepository)
    {
        this.concertPayeeRepository = concertPayeeRepository;
    }

    public async Task HandleAsync(ConcertChangedEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        await concertPayeeRepository.UpsertAsync(e.ConcertId, e.PayeeUserId, ct);
        await concertPayeeRepository.SaveChangesAsync(ct);
    }
}
