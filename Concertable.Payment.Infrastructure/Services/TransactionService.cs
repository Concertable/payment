using Concertable.User.Contracts;

namespace Concertable.Payment.Infrastructure.Services;

internal class TransactionService : ITransactionService
{
    private readonly ITransactionRepository purchaseRepository;
    private readonly ICurrentUser currentUser;
    private readonly ITransactionMapper transactionMapper;

    public TransactionService(
        ICurrentUser currentUser,
        ITransactionRepository purchaseRepository,
        ITransactionMapper transactionMapper)
    {
        this.currentUser = currentUser;
        this.purchaseRepository = purchaseRepository;
        this.transactionMapper = transactionMapper;
    }

    public async Task LogAsync(ITransaction dto)
    {
        var entity = transactionMapper.ToEntity(dto);
        await purchaseRepository.CreateAsync(entity);
    }

    public async Task CompleteAsync(string paymentIntentId)
    {
        var entity = await purchaseRepository.GetByPaymentIntentIdAsync(paymentIntentId);

        if (entity is null)
            return;

        entity.Complete();
        await purchaseRepository.SaveChangesAsync();
    }

    public async Task<IPagination<ITransaction>> GetAsync(IPageParams pageParams)
    {
        var userId = currentUser.GetId();
        var result = await purchaseRepository.GetAsync(pageParams, userId);
        var dtos = transactionMapper.ToDtos(result.Data);
        return new Pagination<ITransaction>(dtos.ToList(), result.TotalCount, result.PageNumber, result.PageSize);
    }
}
