namespace Concertable.Payment.Client;

/// <summary>An owner's default saved card. Property names are the wire contract shared with the SPAs.</summary>
public sealed record SavedCard(string Brand, string Last4, int ExpMonth, int ExpYear);
