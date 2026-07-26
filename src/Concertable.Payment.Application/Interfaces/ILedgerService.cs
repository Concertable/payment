namespace Concertable.Payment.Application.Interfaces;

internal interface ILedgerService
{
    Task PostAsync(LedgerPosting posting, CancellationToken ct = default);
}
