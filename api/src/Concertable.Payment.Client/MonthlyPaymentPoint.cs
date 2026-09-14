using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Client;

public sealed record MonthlyPaymentPoint(DateOnly Month, Money Gross, Money Net, int Count);
