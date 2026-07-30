namespace Concertable.Payment.Domain.Entities;

public sealed class CommissionConfigurationEntity : IGuidEntity
{
    private CommissionConfigurationEntity() { }

    private CommissionConfigurationEntity(
        Guid id,
        string version,
        Currency currency,
        int rateBasisPoints,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
            throw new DomainException("Commission configuration id is required.");
        if (string.IsNullOrWhiteSpace(version))
            throw new DomainException("Commission configuration version is required.");
        if (currency != Currency.Gbp)
            throw new DomainException("Commission configuration currency must be GBP.");
        if (rateBasisPoints is < 1 or > 10_000)
            throw new DomainException("Commission rate must be between 1 and 10,000 basis points.");

        Id = id;
        Version = version;
        Currency = currency;
        RateBasisPoints = rateBasisPoints;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Version { get; private set; } = null!;
    public Currency Currency { get; private set; }
    public int RateBasisPoints { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CommissionConfigurationEntity Create(
        Guid id,
        string version,
        Currency currency,
        int rateBasisPoints,
        DateTimeOffset createdAt) =>
        new(id, version, currency, rateBasisPoints, createdAt);

    public bool HasTerms(Guid id, string version, Currency currency, int rateBasisPoints) =>
        Id == id &&
        string.Equals(Version, version, StringComparison.Ordinal) &&
        Currency == currency &&
        RateBasisPoints == rateBasisPoints;
}
