using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Concertable.Payment.Infrastructure")]
[assembly: InternalsVisibleTo("Concertable.Payment.Api")]
[assembly: InternalsVisibleTo("Concertable.Payment.Testing")]
[assembly: InternalsVisibleTo("Concertable.Testing.Integration")]
[assembly: InternalsVisibleTo("Concertable.B2B.IntegrationTests.Fixtures")]
[assembly: InternalsVisibleTo("Concertable.Payment.UnitTests")]
[assembly: InternalsVisibleTo("Concertable.E2ETests.Api")]
[assembly: InternalsVisibleTo("Concertable.Payment.Seed")]
[assembly: InternalsVisibleTo("Concertable.B2B.Workers.UnitTests")]
// Concert integration tests reference ITransaction via fixture round-trips.
[assembly: InternalsVisibleTo("Concertable.B2B.Concert.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
