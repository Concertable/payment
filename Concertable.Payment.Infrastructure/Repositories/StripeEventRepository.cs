using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal class StripeEventRepository : IStripeEventRepository
{
    private readonly PaymentDbContext context;

    public StripeEventRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<StripeEventEntity?> GetEventByIdAsync(string eventId) =>
        context.StripeEvents.FirstOrDefaultAsync(e => e.EventId == eventId);

    public async Task AddEventAsync(StripeEventEntity stripeEvent)
    {
        await context.StripeEvents.AddAsync(stripeEvent);
        await context.SaveChangesAsync();
    }

    public Task<bool> EventExistsAsync(string eventId) =>
        context.StripeEvents.AnyAsync(e => e.EventId == eventId);
}
