using Concertable.Kernel;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal abstract class BaseRepository<TEntity>(PaymentDbContext context)
    : BaseRepository<TEntity, PaymentDbContext>(context)
    where TEntity : class;

internal abstract class ReadRepository<TEntity>(PaymentDbContext context)
    : ReadRepository<TEntity, PaymentDbContext, int>(context)
    where TEntity : class, IIdEntity;

internal abstract class Repository<TEntity>(PaymentDbContext context)
    : Repository<TEntity, PaymentDbContext, int>(context)
    where TEntity : class, IIdEntity;
