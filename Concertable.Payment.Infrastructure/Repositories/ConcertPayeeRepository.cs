using Concertable.Kernel.Exceptions;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal interface IConcertPayeeRepository
{
    Task<Guid> GetPayeeUserIdAsync(int concertId, CancellationToken ct = default);
    Task UpsertAsync(int concertId, Guid payeeUserId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

internal class ConcertPayeeRepository : IConcertPayeeRepository
{
    private readonly PaymentDbContext context;

    public ConcertPayeeRepository(PaymentDbContext context)
    {
        this.context = context;
    }

    public async Task<Guid> GetPayeeUserIdAsync(int concertId, CancellationToken ct = default)
    {
        var entity = await context.ConcertPayees.FirstOrDefaultAsync(x => x.ConcertId == concertId, ct)
            ?? throw new NotFoundException($"No payee routing found for concert {concertId}");
        return entity.PayeeUserId;
    }

    public async Task UpsertAsync(int concertId, Guid payeeUserId, CancellationToken ct = default)
    {
        var entity = await context.ConcertPayees.FirstOrDefaultAsync(x => x.ConcertId == concertId, ct);
        if (entity is null)
            await context.ConcertPayees.AddAsync(ConcertPayeeEntity.Create(concertId, payeeUserId), ct);
        else
            entity.Update(payeeUserId);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);
}
