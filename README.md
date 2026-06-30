# Concertable.Payment

The **Payment** service of [Concertable](https://github.com/Concertable/concertable) — the payments
*adapter service*: it fronts Stripe for checkout, webhooks, and connected-account payouts. It is an
agnostic adapter — it owns no seed catalog and emits payment events only for *live* Stripe webhooks,
never for seed data. As an adapter, data services may call it synchronously and `WaitFor` it at
startup.

## Canonical source vs. this mirror

Development happens in the **monorepo** ([`Concertable/concertable`](https://github.com/Concertable/concertable)),
under `api/Concertable.Payment/`. That folder is **automatically mirrored** to the read-only repo
[`Concertable/concertable-payment`](https://github.com/Concertable/concertable-payment) on every
push to `master`. **Don't open PRs against the mirror** — nothing flows back from it.

## Building standalone

The deployable closure consumes Concertable's shared platform as NuGet `PackageReference`s from the
private org feed `https://nuget.pkg.github.com/Concertable`. Restoring them needs a GitHub
[personal access token](https://github.com/settings/tokens) with the **`read:packages`** scope,
exported as `GITHUB_PACKAGES_TOKEN` (the `nuget.config` reads it):

```sh
export GITHUB_PACKAGES_TOKEN=<your read:packages PAT>
dotnet build Concertable.Payment.Web/Concertable.Payment.Web.csproj
dotnet build Concertable.Payment.Workers/Concertable.Payment.Workers.csproj
```

Building the two host projects pulls the whole deployable closure. (In the monorepo's CI the same
variable is supplied by the workflow's `GITHUB_TOKEN`; standalone, you export your own PAT.)
