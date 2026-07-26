using Concertable.Contracts;
using Concertable.Kernel.Identity;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class TransactionService : ITransactionService
{
    private readonly ITransactionRepository purchaseRepository;
    private readonly ICurrentUser currentUser;
    private readonly ITransactionMapper transactionMapper;
    private readonly ILedgerService ledger;

    public TransactionService(
        ICurrentUser currentUser,
        ITransactionRepository purchaseRepository,
        ITransactionMapper transactionMapper,
        ILedgerService ledger)
    {
        this.currentUser = currentUser;
        this.purchaseRepository = purchaseRepository;
        this.transactionMapper = transactionMapper;
        this.ledger = ledger;
    }

    public async Task LogAsync(ITransaction dto)
    {
        var entity = transactionMapper.ToEntity(dto);
        await purchaseRepository.CreateAsync(entity);
    }

    public async Task CompleteAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var entity = await purchaseRepository.GetByPaymentIntentIdAsync(paymentIntentId);

        if (entity is null || !entity.Complete())
            return;

        if (entity is SettlementTransactionEntity settlement)
        {
            await ledger.PostAsync(LedgerPostings.DirectSettlement(settlement), ct);
            return;
        }

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
