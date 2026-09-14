namespace Concertable.Payment.Application.Requests;

internal sealed record TransferReversal(string TransferId, Money Amount);
