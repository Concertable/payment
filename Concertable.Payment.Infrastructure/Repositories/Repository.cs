using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure.Repositories;

internal abstract class BaseRepository<TEntity>(PaymentDbContext context)
    : BaseRepository<TEntity, PaymentDbContext>(context)
    where TEntity : class;

internal abstract class Repository<TEntity>(PaymentDbContext context)
    : Repository<TEntity, PaymentDbContext>(context)
    where TEntity : class, IIdEntity;

internal abstract class GuidRepository<TEntity>(PaymentDbContext context)
    : GuidRepository<TEntity, PaymentDbContext>(context)
    where TEntity : class, IGuidEntity;
