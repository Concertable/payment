using Concertable.Contracts;

namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionService
{
    Task LogAsync(ITransaction dto);
    Task CompleteAsync(string paymentIntentId, CancellationToken ct = default);
    Task<IPagination<ITransaction>> GetAsync(IPageParams pageParams);
}
