using Concertable.Contracts;
using Concertable.Kernel.Identity;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class TransactionService : ITransactionService
{
    private readonly ITransactionRepository purchaseRepository;
    private readonly ICurrentUser currentUser;
    private readonly ITransactionMapper transactionMapper;
    private readonly ILedgerService ledger;
    private readonly IUnitOfWork unitOfWork;
    private readonly TimeProvider timeProvider;

    public TransactionService(
        ICurrentUser currentUser,
        ITransactionRepository purchaseRepository,
        ITransactionMapper transactionMapper,
        ILedgerService ledger,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        this.currentUser = currentUser;
        this.purchaseRepository = purchaseRepository;
        this.transactionMapper = transactionMapper;
        this.ledger = ledger;
        this.unitOfWork = unitOfWork;
        this.timeProvider = timeProvider;
    }

    public async Task LogAsync(ITransaction dto)
    {
        var entity = transactionMapper.ToEntity(dto);
        await purchaseRepository.AddAsync(entity);
    }

    public async Task CompleteAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var entity = await purchaseRepository.GetByPaymentIntentIdAsync(paymentIntentId);

        if (entity is null)
            return;

        if (entity.Complete(timeProvider.GetUtcNow().UtcDateTime).IsFailure)
            return;

        if (entity is SettlementTransactionEntity settlement)
        {
            await ledger.StageAsync(LedgerPostings.DirectSettlement(settlement), ct);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        await purchaseRepository.SaveChangesAsync();
    }

    public async Task<IPagination<ITransaction>> GetAsync(IPageParams pageParams)
    {
        var userId = currentUser.GetId();
        var result = await purchaseRepository.GetAsync(pageParams, userId);
        return result.Map(transactionMapper.ToDto);
    }
}
