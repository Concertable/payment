namespace Concertable.Payment.Domain;

public sealed record CommissionTerms(
    Guid ConfigurationId,
    string Version,
    Currency Currency,
    int RateBasisPoints,
    int VatRateBasisPoints);
