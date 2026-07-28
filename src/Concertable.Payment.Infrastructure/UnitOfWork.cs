using Concertable.DataAccess.Application;
using Concertable.DataAccess.Infrastructure;
using Concertable.Payment.Infrastructure.Data;

namespace Concertable.Payment.Infrastructure;

internal interface IUnitOfWork : IUnitOfWork<PaymentDbContext>;

internal sealed class UnitOfWork(PaymentDbContext context)
    : UnitOfWork<PaymentDbContext>(context), IUnitOfWork;
