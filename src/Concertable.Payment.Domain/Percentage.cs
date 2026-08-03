namespace Concertable.Payment.Domain;

internal readonly record struct Percentage
{
    private Percentage(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }
    public bool IsZero => Value == 0m;

    public static Percentage From(decimal value)
    {
        if (value is < 0m or > 100m)
            throw new DomainException("Percentage must be between 0 and 100.");
        if (decimal.Round(value, 4) != value)
            throw new DomainException("Percentage cannot have more than four decimal places.");

        return new Percentage(value);
    }

    public long ApplyTo(long amount) => RoundHalfUp(amount * Value / 100m);

    public long ExcludeFrom(long inclusiveAmount) =>
        RoundHalfUp(inclusiveAmount * 100m / (100m + Value));

    private static long RoundHalfUp(decimal value) =>
        decimal.ToInt64(decimal.Round(value, 0, MidpointRounding.AwayFromZero));
}
