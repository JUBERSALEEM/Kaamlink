# ADR 0002: Backend = ASP.NET Core modular monolith + PostgreSQL

## Status
Accepted (Phase 01 — recommendation for Phase 02 implementation)

## Context
No backend exists. V1 needs: secure phone-OTP auth, role-based authorization
(Worker; company-scoped OWNER/ADMIN/HIRING_MANAGER/SUPERVISOR/VIEWER; platform admins),
relational hiring data, document storage, push notifications, offline-sync endpoints,
future web-portal reuse, audit logging — at near-zero initial cost (10→5,000 users).

## Decision
Build the backend as a **modular monolith**:
- **ASP.NET Core 10 Web API** (controllers or minimal APIs backed by an application layer)
- **PostgreSQL 16** via **EF Core 10** with code-first migrations; transactions enforced
  at application-service boundaries
- **Object storage** for documents (S3-compatible: Cloudflare R2 or Backblaze B2 to start)
- **Firebase Cloud Messaging** for Android push (free)
- One deployable unit on one small host; modules split only when a real scaling/team
  boundary appears.

Explicitly rejected for V1: microservices, Kubernetes, service buses, multi-country
sharding (see report §56 — no premature infrastructure).

## Rationale
- Relational core fits the domain (multi-worker vacancies, pipelines, assessments,
  audit trails) with constraints/indexes/migrations the client cannot fake.
- Same language as the client; shared contract project possible; deep boring
  ecosystem (auth, rate limiting, health checks, OpenAPI).
- PostgreSQL runs free/cheap everywhere and scales vertically well past MVP needs;
  managed upgrade path exists on every cloud.
- Deployable on a €5–15 VPS or PaaS free tiers; swap modules later without client changes
  because clients only see versioned REST + auth.

## Consequences
- Must design module boundaries (Auth/Identity, Profiles, Companies, Jobs, Applications,
  Assessments, Documents, Messaging, Admin) with internal interfaces from day one so
  extraction is possible later.
- Server-side pagination/filtering contracts must exist before Android lists are built.
- Secrets via environment/secret manager only; per-environment isolation (dev/stage/prod).
