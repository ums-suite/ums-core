# ums-core

The University Management System backend — a **.NET modular monolith** hosting all 15 bounded-context modules behind one deployable API (`kart-commerce`'s counterpart, run the opposite way: one deployment, disciplined internal boundaries instead of 20 services).

## Modules

Identity, Organization, Admission, Academic, Student, Faculty, Finance, Hostel, Library, Alumni, Content, Documents, Notifications, Reporting, Audit — see [`ums-platform`'s module boundary map](https://github.com/ums-suite/ums-platform/blob/main/docs/architecture/module-boundaries.md) for the dependency graph and [`docs/services/`](https://github.com/ums-suite/ums-platform/tree/main/docs/services) for each module's full requirement spec.

## Tech Stack

.NET 10 (ASP.NET Core Web API) · PostgreSQL (one database, schema per module) · Redis (cache/session/coordination) · Docker · Kubernetes.

## Architecture

Governed by [`ums-platform`](https://github.com/ums-suite/ums-platform)'s ADRs, most directly:

- [ADR-0001](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0001-modular-monolith-over-microservices.md) — why modular monolith
- [ADR-0002](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0002-module-boundary-enforcement.md) — how module boundaries are enforced in CI, not just convention
- [ADR-0004](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0004-postgres-schema-per-module.md) — one database, one schema per module

## Status

Requirements and design complete for every module (`ums-platform/docs/services/`). Code generation happens one module at a time, prompted directly against that module's `requirement-spec.md` — see [`PLATFORM_BLUEPRINT.md`](https://github.com/ums-suite/ums-platform/blob/main/PLATFORM_BLUEPRINT.md) §3. No module code has been generated yet.
