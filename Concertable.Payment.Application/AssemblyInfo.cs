using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Concertable.Payment.Infrastructure")]
[assembly: InternalsVisibleTo("Concertable.Payment.Api")]
[assembly: InternalsVisibleTo("Concertable.Testing.Integration")]
[assembly: InternalsVisibleTo("Concertable.Testing.Integration.B2B")]
[assembly: InternalsVisibleTo("Concertable.Payment.UnitTests")]
[assembly: InternalsVisibleTo("Concertable.E2ETests.Api")]
[assembly: InternalsVisibleTo("Concertable.Workers.UnitTests")]
// Concert.Infrastructure uses IStripeValidator + IStripeValidationFactory in
// OpportunityService/ApplicationService for pre-create/pre-apply Stripe eligibility checks.
// TEMPORARY until eligibility routes through a Payment.Contracts facade.
[assembly: InternalsVisibleTo("Concertable.B2B.Concert.Infrastructure")]
// Concert integration tests reference ITransaction via fixture round-trips.
[assembly: InternalsVisibleTo("Concertable.B2B.Concert.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
