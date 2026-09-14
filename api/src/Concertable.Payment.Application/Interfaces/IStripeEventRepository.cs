namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeEventRepository
{
    Task<StripeEventEntity?> GetEventByIdAsync(string eventId);
    void AddEvent(StripeEventEntity stripeEvent);
    Task<bool> EventExistsAsync(string eventId);
}
