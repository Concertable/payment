using Concertable.DataAccess.Application;
using Concertable.DataAccess.Infrastructure;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure;

internal interface IOutboxUnitOfWorkBehavior : IOutboxUnitOfWorkBehavior<PaymentDbContext>;

internal sealed class OutboxUnitOfWorkBehavior(PaymentDbContext context, IDbContextAccessor accessor)
    : OutboxUnitOfWorkBehavior<PaymentDbContext>(context, accessor), IOutboxUnitOfWorkBehavior;
