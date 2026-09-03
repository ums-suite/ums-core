# ums-core

The University Management System backend — a **.NET modular monolith** hosting all 18 bounded-context modules behind one deployable API (`kart-commerce`'s counterpart, run the opposite way: one deployment, disciplined internal boundaries instead of 20 services).

## Modules

Identity, Organization, Admission, Academic, Student, Faculty, Finance, Hostel, Library, Alumni, Content, Documents, Notifications, Reporting, Audit, Learning, Research, Career — see [`ums-platform`'s module boundary map](https://github.com/ums-suite/ums-platform/blob/main/docs/architecture/module-boundaries.md) for the dependency graph and [`docs/services/`](https://github.com/ums-suite/ums-platform/tree/main/docs/services) for each module's full requirement spec.

## Tech Stack

.NET 10 (ASP.NET Core Web API) · PostgreSQL (one database, schema per module) · Redis (cache/session/coordination) · Docker · Kubernetes.

## Architecture

Governed by [`ums-platform`](https://github.com/ums-suite/ums-platform)'s ADRs, most directly:

- [ADR-0001](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0001-modular-monolith-over-microservices.md) — why modular monolith
- [ADR-0002](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0002-module-boundary-enforcement.md) — how module boundaries are enforced in CI, not just convention
- [ADR-0004](https://github.com/ums-suite/ums-platform/blob/main/docs/adr/0004-postgres-schema-per-module.md) — one database, one schema per module

## Status

Requirements and design complete for every module (`ums-platform/docs/services/`). Code generation happens one module at a time, prompted directly against that module's `requirement-spec.md` — see [`PLATFORM_BLUEPRINT.md`](https://github.com/ums-suite/ums-platform/blob/main/PLATFORM_BLUEPRINT.md) §3.

The solution scaffold (`release/DEVELOPMENT_PLAN.md` Flow #2) is done: `UMS.Core.slnx`, the shared cross-cutting libraries every module will depend on, the `UMS.Host`/`UMS.Workers` deployables, module-boundary architecture tests, and CI/Docker/docker-compose wiring. No module exists yet — `src/UMS.Modules/` is empty until Identity (Flow #4).

## Getting Started

```bash
# 1. Bring up Postgres/Redis/MinIO (+ ums-core/UMS.Workers themselves, once you've built them):
cd ../ums-devops && scripts/dev-up.sh

# 2. Or run UMS.Host directly against that infra (faster inner loop than a full image rebuild):
dotnet run --project src/Host

# 3. Verify:
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready   # checks real Postgres + Redis connectivity

dotnet build UMS.Core.slnx
dotnet format UMS.Core.slnx --verify-no-changes
dotnet test UMS.Core.slnx
```

## Solution Layout

```
UMS.Core.slnx
src/
  Host/                       UMS.Host - the single ums-core deployable (ADR-0001)
  Workers/UMS.Workers/        background job runner (ADR-0014), a separate deployable
  Shared/
    UMS.Shared.Observability/ Serilog + OpenTelemetry, wired once (correlation-id middleware included)
    UMS.Shared.ErrorHandling/ Result<T> pattern, the global exception handler, ProblemDetails envelope
    UMS.Shared.Resilience/    Polly-backed HttpClient resilience, Redis-backed rate limiting
  UMS.Modules/                empty until Identity (Flow #4) - see module-boundaries.md
tests/
  ArchitectureTests/          NetArchTest-based ADR-0002 module-boundary enforcement
  UMS.Shared.Tests/           unit tests for the shared libraries above
```
