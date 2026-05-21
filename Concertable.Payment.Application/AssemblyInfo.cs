using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Concertable.Payment.Infrastructure")]
[assembly: InternalsVisibleTo("Concertable.Payment.Api")]
// TEMPORARY: legacy hosts still consume Payment internals until Steps 7/8/12 retire them.
// Concertable.Infrastructure entry retired Step 10 (legacy payment service files all deleted).
[assembly: InternalsVisibleTo("Concertable.B2B.Web")]
[assembly: InternalsVisibleTo("Concertable.B2B.Workers")]
[assembly: InternalsVisibleTo("Concertable.Testing.Integration")]
[assembly: InternalsVisibleTo("Concertable.Payment.UnitTests")]
[assembly: InternalsVisibleTo("Concertable.E2ETests.Api")]
[assembly: InternalsVisibleTo("Concertable.Workers.UnitTests")]
// Concert.Infrastructure uses IStripeValidator + IStripeValidationFactory in
// OpportunityService/ApplicationService for pre-create/pre-apply Stripe eligibility checks.
// TEMPORARY until eligibility routes through a Payment.Contracts facade.
[assembly: InternalsVisibleTo("Concertable.Concert.Infrastructure")]
// Concert integration tests reference ITransaction via fixture round-trips.
[assembly: InternalsVisibleTo("Concertable.Concert.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
