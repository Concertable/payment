namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeEventRepository
{
    Task<StripeEventEntity?> GetEventByIdAsync(string eventId);
    Task AddEventAsync(StripeEventEntity stripeEvent);
    Task<bool> EventExistsAsync(string eventId);
}
