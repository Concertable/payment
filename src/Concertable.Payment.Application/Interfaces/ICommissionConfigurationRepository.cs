using Concertable.DataAccess.Application;

namespace Concertable.Payment.Application.Interfaces;

internal interface ICommissionConfigurationRepository : IRepository<CommissionConfigurationEntity, Guid>;
