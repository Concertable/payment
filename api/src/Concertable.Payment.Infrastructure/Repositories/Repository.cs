using Concertable.Kernel;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal abstract class Repository<TEntity>(PaymentDbContext context)
    : Repository<TEntity, int>(context)
    where TEntity : class, IIdEntity;

internal abstract class GuidRepository<TEntity>(PaymentDbContext context)
    : Repository<TEntity, Guid>(context)
    where TEntity : class, IGuidEntity;
