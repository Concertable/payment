# Concertable.Payment.Seed

## `StripeE2EAccountResolver` must cover every seeded user with a Stripe ID

The dictionaries in `StripeE2EAccountResolver.cs` should contain a mapping for every seeded user that has a real Stripe test-mode account — every `SeedUsers.ArtistManagerId(i)` and `SeedUsers.VenueManagerId(i)` for `i ∈ [1, SeedUsers.ManagerCount]`, plus every customer in `SeedCustomers.All` that has saved test cards.

If you find the resolver doesn't cover all of them, fill it in. **Don't reason around it.** A missing entry is a bug here, not a feature.

## `[payment].[PayoutAccounts]` row count is not an event-delivery count

`E2EStripeAccountClient.Provision*Async` calls `resolver.TryGet*` and returns silently on a miss — so a `CredentialRegisteredEvent` for a manager not in the resolver produces zero `PayoutAccount` rows even though the event was delivered and the handler ran end-to-end.

Don't use `PayoutAccounts` row counts to argue about whether the event pipeline is healthy. To check actual delivery, query `[messaging].[Inbox]` in `PaymentDb` filtered by `ConsumerName` — that row is written at the top of the handler before any short-circuit.
