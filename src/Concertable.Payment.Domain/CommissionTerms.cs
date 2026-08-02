namespace Concertable.Payment.Domain;

public sealed record CommissionTerms(
    Guid ConfigurationId,
    Percentage Rate);
