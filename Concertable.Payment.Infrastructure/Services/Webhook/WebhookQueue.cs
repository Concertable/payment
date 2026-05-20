using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Shared;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services.Webhook;

internal class WebhookQueue : IWebhookQueue
{
    private readonly IBackgroundTaskQueue taskQueue;
    private readonly IServiceScopeFactory scopeFactory;

    public WebhookQueue(IBackgroundTaskQueue taskQueue, IServiceScopeFactory scopeFactory)
    {
        this.taskQueue = taskQueue;
        this.scopeFactory = scopeFactory;
    }

    public Task EnqueueAsync(Event stripeEvent)
    {
        return taskQueue.EnqueueAsync(async ct =>
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IWebhookProcessor>();
            await processor.ProcessAsync(stripeEvent, ct);
        });
    }
}
