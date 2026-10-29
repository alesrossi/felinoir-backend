# 0002 — Clean architecture with CQRS-lite feature slices

**Status:** accepted · 2026-10-28 · supersedes the "service pattern" and "no repository / unit
of work" rules in `CLAUDE.md`

## Context

The first version used `I*Service` classes over an injected `DbContext`. As features grew
(Lobbies, Felix), business rules spread across services, endpoints and static helpers, and the
services kept growing. We want a structure that keeps the clean-architecture dependency rule,
applies DDD where there are invariants to protect, and handles cross-cutting concerns (logging,
validation, correlation ids, transactions) in one place.

About 90% of the traffic is reads of data written by the scrapers (Movies, Cinemas, Screenings).
In this process those are read models with nothing to protect. The real write model is Lobbies.

## Decision

- Keep the four projects (Domain, Application, Infrastructure, Api) and the dependency rule.
- Organise the Application layer by feature slice: one request with one handler per use case
  (`Features/<Area>/<UseCase>/`), dispatched through `martinothamar/Mediator` (MIT,
  source-generated). MediatR was ruled out because of its commercial licence since v13.
- **Commands** load aggregates through one repository per aggregate root, change them through
  domain methods, and commit through `IUnitOfWork` (in a pipeline behaviour).
- **Queries** skip aggregates and repositories. They project straight from a no-tracking
  `DbContext` into response records.
- Cross-cutting concerns are mediator pipeline behaviours, so they also apply to work that
  doesn't come in over HTTP (background jobs).

## Consequences

- Reads bypass repositories on purpose. A repository is never added just to serve a query.
- Aggregates exist only where there are invariants. Catalogue entities stay simple.
- Navigation goes by naming convention (`XCommand` next to `XHandler`), not "go to definition".
- `CLAUDE.md` has to be rewritten to match.
