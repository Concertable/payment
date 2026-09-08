# Concertable.Payment.StartupTests — startup tests

**Would any Payment host refuse to boot, given the configuration its app model supplies it?** These build the
real production registration graphs (Web, Workers) and the AppHost's resource graph without starting any of
them or reaching external infrastructure. Tests that execute requests, business operations or infrastructure
belong in integration or E2E projects; tests that assert code structure belong in
`Concertable.Payment.ArchitectureTests`.

Host coverage and activation rules: the `composition-testing` skill.
