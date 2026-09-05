using Concertable.DataAccess.Infrastructure.Data;
using Concertable.Kernel;
using Concertable.Kernel.Events;
using Concertable.Messaging.Contracts;
using Concertable.Messaging.Domain;
using Concertable.Messaging.Infrastructure.Extensions;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Events;
using Concertable.Payment.Domain.Lifecycle;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Events;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class WebhookReconciliationHarness : IAsyncDisposable
{
    private static readonly string StateChangedMessageType =
        MessageTypeAttribute.Resolve(typeof(PaymentOperationStateChanged));

    private readonly ServiceProvider provider;

    private WebhookReconciliationHarness(ServiceProvider provider, FakeStripeSessionClient sessionClient)
    {
        this.provider = provider;
        this.SessionClient = sessionClient;
    }

    public FakeStripeSessionClient SessionClient { get; }

    public static async Task<WebhookReconciliationHarness> CreateAsync(string connectionString)
    {
        var sessionClient = new FakeStripeSessionClient(TimeProvider.System);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PaymentConfigurationProvider>();
        services.AddSingleton<IStripeSessionClient>(sessionClient);

        services.AddOutbox(opt => opt.UseSqlServer(connectionString), runDispatcher: false);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventDispatchInterceptor, DomainEventDispatchInterceptor>();
        services.AddScoped<IDomainEventHandler<PaymentOperationStateChangedDomainEvent>,
            PaymentOperationStateChangedDomainEventHandler>();

        services.AddDbContext<PaymentDbContext>((sp, opts) =>
            opts.UseSqlServer(connectionString)
                .AddInterceptors(sp.GetRequiredService<IDomainEventDispatchInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxUnitOfWorkBehavior, OutboxUnitOfWorkBehavior>();
        services.AddScoped<IStripeEventRepository, StripeEventRepository>();
        services.AddScoped<IPayoutAccountRepository, PayoutAccountRepository>();
        services.AddScoped<IPaymentSessionOperationRepository, PaymentSessionOperationRepository>();
        services.AddScoped<IPaymentSessionAttemptRepository, PaymentSessionAttemptRepository>();
        services.AddScoped<IPaymentOperationResolver, PaymentOperationResolver>();
        services.AddSingleton<PaymentSessionStateMachine>();
        services.AddScoped<IPaymentSessionReconciliationService, PaymentSessionReconciliationService>();
        services.AddScoped<IPaymentSessionResourceReconciler, PaymentSessionResourceReconciler>();
        services.AddScoped<PaymentSessionService>();
        services.AddScoped<IStripeWebhookHandler<PaymentIntent>, PaymentIntentWebhookHandler>();
        services.AddScoped<IStripeWebhookHandler<SetupIntent>, SetupIntentWebhookHandler>();
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.MigrateAsync();
        }

        return new WebhookReconciliationHarness(provider, sessionClient);
    }

    public async Task<PaymentSessionExecution> CreateSessionAsync(PaymentSessionDefinition specification)
    {
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<PaymentSessionService>()
            .CreateAsync(specification);
        Assert.True(result.TryGetValue(out var execution));
        return execution;
    }

    public async Task ProcessWebhookAsync(Event stripeEvent)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWebhookProcessor>()
            .ProcessAsync(stripeEvent, CancellationToken.None);
    }

    public async Task<PaymentSessionAttemptEntity> GetCurrentAttemptAsync(Guid operationId)
    {
        using var scope = provider.CreateScope();
        var operation = await scope.ServiceProvider.GetRequiredService<IPaymentSessionOperationRepository>()
            .GetByOperationIdAsync(operationId);
        Assert.NotNull(operation);
        return operation.CurrentAttempt;
    }

    public async Task<int> StateChangeCountAsync(Guid operationId)
    {
        var operationKey = operationId.ToString();
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDbContext>()
            .Set<OutboxMessageEntity>()
            .CountAsync(message =>
                message.MessageType == StateChangedMessageType
                && message.Payload.Contains(operationKey));
    }

    public async Task<int> PaymentSucceededCountAsync()
    {
        var succeededMessageType = MessageTypeAttribute.Resolve(typeof(PaymentSucceededEvent));
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDbContext>()
            .Set<OutboxMessageEntity>()
            .CountAsync(message => message.MessageType == succeededMessageType);
    }

    public async Task<int> PaymentSucceededCountAsync(string clientReference)
    {
        var succeededMessageType = MessageTypeAttribute.Resolve(typeof(PaymentSucceededEvent));
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDbContext>()
            .Set<OutboxMessageEntity>()
            .CountAsync(message =>
                message.MessageType == succeededMessageType
                && message.Payload.Contains(clientReference));
    }

    public ValueTask DisposeAsync() => provider.DisposeAsync();
}
