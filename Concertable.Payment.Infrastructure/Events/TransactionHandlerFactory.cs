using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class TransactionHandlerFactory : ITransactionHandlerFactory
{
    private readonly IKeyedServiceProvider serviceProvider;
    private readonly ILogger<TransactionHandlerFactory> logger;

    public TransactionHandlerFactory(
        IKeyedServiceProvider serviceProvider,
        ILogger<TransactionHandlerFactory> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public ITransactionHandler Create(string type)
    {
        var handler = serviceProvider.GetKeyedService<ITransactionHandler>(type);
        if (handler is null)
        {
            logger.LogError(
                "No ITransactionHandler is registered for transaction type {TransactionType}. Check ServiceCollectionExtensions for AddKeyedScoped registrations.",
                type);
            throw new InvalidOperationException(
                $"No ITransactionHandler registered for transaction type '{type}'.");
        }
        return handler;
    }
}
