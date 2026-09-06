using Concertable.Payment.Contracts;

namespace Concertable.Payment.Hosting;

public static class PaymentConstants
{
    public const string Database = "PaymentDb";
    public const string WebResource = "payment-web";
    public const string WorkersResource = "payment-workers";
    public const string StripeCliResource = "stripe-cli";
    public const string ServiceName = PaymentServiceIdentity.Name;
    public const int HttpPort = 8080;
    public const int GrpcPort = 8081;
}
