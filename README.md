# Concertable.Payment

The **Payment** service of [Concertable](https://github.com/Concertable/concertable) — the payments
*adapter service*: it fronts Stripe for checkout, webhooks, and connected-account payouts. It is an
agnostic adapter — it owns no seed catalog and emits payment events only for *live* Stripe webhooks,
never for seed data. As an adapter, data services may call it synchronously and `WaitFor` it at
startup.

## Repository status

This private repository is the Payment service's polyrepo preparation target. Repository-owned CI and
release plumbing are prepared here before the approved canonical cutover. The Concertable monorepo remains
the production source of truth until that cutover; preparation changes belong in this repository and must
not publish canonical packages or images, change repository visibility, or deploy production early.

## Building standalone

The deployable closure consumes Concertable's shared platform as NuGet `PackageReference`s from the
private org feed `https://nuget.pkg.github.com/Concertable`. Restoring them needs a GitHub
[personal access token](https://github.com/settings/tokens) with the **`read:packages`** scope,
exported as `GITHUB_PACKAGES_TOKEN` (the `nuget.config` reads it):

```sh
export GITHUB_PACKAGES_TOKEN=<your read:packages PAT>
dotnet build src/Concertable.Payment.Web/Concertable.Payment.Web.csproj
dotnet build src/Concertable.Payment.Workers/Concertable.Payment.Workers.csproj
```

Building the two host projects pulls the whole deployable closure. Repository CI supplies the same variable
from its read-only `GITHUB_TOKEN`; standalone, you export your own PAT.

The extracted AppHost and E2E helpers still contain composition/test source references owned by the wider
polyrepo migration. They remain outside repository CI until those dependencies are replaced by published
Hosting/TestKit packages and pinned images. Architecture tests are repository-local and run in CI; they
validate the Payment Web and Workers production registration graphs without starting either host.
