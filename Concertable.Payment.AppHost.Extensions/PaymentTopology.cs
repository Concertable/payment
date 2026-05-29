public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe("event-concertchangedevent",       "payment-concert-changed",       "concertable-payment")
            .Subscribe("event-credentialregisteredevent", "payment-credential-registered", "concertable-payment")
            .Subscribe("event-paymentsucceededevent",     "payment-payment-succeeded",     "concertable-payment")
            .Subscribe("event-paymentfailedevent",        "payment-payment-failed",        "concertable-payment");
}
