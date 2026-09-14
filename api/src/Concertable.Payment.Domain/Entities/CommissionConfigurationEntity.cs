namespace Concertable.Payment.Domain.Entities;

internal sealed class CommissionConfigurationEntity : IGuidEntity
{
    private CommissionConfigurationEntity() { }

    private CommissionConfigurationEntity(
        Guid id,
        Percentage rate,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("Commission configuration id is required.");
        if (rate.IsZero)
            throw new DomainException("Commission rate must be greater than zero.");

        Id = id;
        Rate = rate;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Percentage Rate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public CommissionTerms Terms => new(Id, Rate);

    public static CommissionConfigurationEntity Create(
        Guid id,
        Percentage rate,
        DateTimeOffset createdAt) =>
        new(id, rate, createdAt);

    public bool Matches(Percentage rate) => Rate == rate;
}
