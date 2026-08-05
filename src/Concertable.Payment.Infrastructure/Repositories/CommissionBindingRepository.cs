using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class CommissionBindingRepository
    : GuidRepository<CommissionBindingEntity>, ICommissionBindingRepository
{
    public CommissionBindingRepository(PaymentDbContext context)
        : base(context) { }

    public override Task<CommissionBindingEntity?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        context.CommissionBindings
            .Include(a => a.CommissionConfiguration)
            .SingleOrDefaultAsync(a => a.Id == id, ct);

    public async Task<CommissionBindingEntity> GetOrCreateAsync(
        CommissionBindingEntity candidate,
        CancellationToken ct = default)
    {
        var binding = await context.CommissionBindings.GetOrCreateAsync(
            candidate,
            a => new { a.ExternalReference, a.PayerReference },
            a => a.ExternalReference == candidate.ExternalReference &&
                 a.PayerReference == candidate.PayerReference,
            ct);
        await context.Entry(binding)
            .Reference(a => a.CommissionConfiguration)
            .LoadAsync(ct);
        return binding;
    }

    public async Task<bool> TryConfirmReviewedGrossAsync(
        Guid bindingId,
        Money reviewedGross,
        CancellationToken ct = default)
    {
        var reviewedGrossMinor = reviewedGross.ToMinorUnits();
        var affected = await context.CommissionBindings
            .Where(binding =>
                binding.Id == bindingId &&
                binding.Currency == reviewedGross.Currency &&
                (binding.ReviewedGrossMinor == null || binding.ReviewedGrossMinor == reviewedGrossMinor))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(binding => binding.ReviewedGrossMinor, reviewedGrossMinor),
                ct);
        return affected == 1;
    }
}
