using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class FinancialOperationRepository : IFinancialOperationRepository
{
    private readonly PaymentDbContext context;

    public FinancialOperationRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task<FinancialOperationEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        context.FinancialOperations.SingleOrDefaultAsync(operation => operation.Id == id, ct);

    public Task AddAsync(FinancialOperationEntity operation, CancellationToken ct = default) =>
        context.FinancialOperations.AddAsync(operation, ct).AsTask();
}
