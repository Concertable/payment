public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe("event-concertchangedevent",       "payment-concert-changed",       "concertable-payment")
            .Subscribe("event-credentialregisteredevent", "payment-credential-registered", "concertable-payment")
            .Subscribe("event-tenantcreatedevent",        "payment-tenant-created",        "concertable-payment")
            .Subscribe("event-paymentsucceededevent",     "payment-payment-succeeded",     "concertable-payment")
            .Subscribe("event-paymentfailedevent",        "payment-payment-failed",        "concertable-payment")
            // Both names exist across the service-scoped queue rename: Payment keeps using the
            // unscoped name until platform-sync pins it to the Messaging version that scopes it.
            .Queue("command-processstripewebhookcommand")
            .Queue("command-concertable-payment-processstripewebhookcommand");
}
