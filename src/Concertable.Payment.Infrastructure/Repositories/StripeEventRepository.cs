using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class StripeEventRepository : IStripeEventRepository
{
    private readonly PaymentDbContext context;

    public StripeEventRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<StripeEventEntity?> GetEventByIdAsync(string eventId) =>
        context.StripeEvents.FirstOrDefaultAsync(e => e.EventId == eventId);

    public void AddEvent(StripeEventEntity stripeEvent) =>
        context.StripeEvents.Add(stripeEvent);

    public Task<bool> EventExistsAsync(string eventId) =>
        context.StripeEvents.AnyAsync(e => e.EventId == eventId);
}
