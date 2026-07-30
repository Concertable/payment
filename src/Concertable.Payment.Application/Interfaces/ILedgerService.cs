namespace Concertable.Payment.Application.Interfaces;

internal interface ILedgerService
{
    Task StageAsync(LedgerPosting posting, CancellationToken ct = default);
}
