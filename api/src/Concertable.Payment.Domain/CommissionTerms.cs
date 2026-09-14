namespace Concertable.Payment.Domain;

internal sealed record CommissionTerms(
    Guid ConfigurationId,
    Percentage Rate);
