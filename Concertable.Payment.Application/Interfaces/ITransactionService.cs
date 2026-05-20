namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionService
{
    Task LogAsync(ITransaction dto);
    Task CompleteAsync(string paymentIntentId);
    Task<IPagination<ITransaction>> GetAsync(IPageParams pageParams);
}
