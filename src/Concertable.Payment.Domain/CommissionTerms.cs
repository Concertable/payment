namespace Concertable.Payment.Domain;

internal sealed record CommissionTerms(
    Guid ConfigurationId,
    string Version,
    Currency Currency,
    int RateBasisPoints,
    int VatRateBasisPoints);
