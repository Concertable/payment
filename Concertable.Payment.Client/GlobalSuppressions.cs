using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "MA0053:Make class or record sealed", Scope = "type", Target = "~T:Concertable.Payment.Client.PaymentResponse", Justification = "Subclassed by TicketPaymentResponse in the Customer service, which the analyzer cannot see.")]
