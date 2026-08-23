# ADR 0003: Authentication identity is separate from business profiles

## Status
Accepted (Phase 01)

## Context
The platform has two fundamentally different product experiences (Worker, Employer),
and one person may legitimately be both over time. Companies are legal entities with
multiple human members of different authority levels. Storing role/business data on the
auth user record (or assuming 1 account = 1 company) creates irreversible data-model debt.

## Decision
- `User` (auth identity: phone, credentials, status) owns zero business data.
- `WorkerProfile` belongs to a `User` 1:0..1 — the worker's identity is independent of
  any employer and survives job changes.
- `Company` is a first-class entity; `CompanyMember(User, Company, Role)` is the
  join with roles OWNER / ADMIN / HIRING_MANAGER / SUPERVISOR / VIEWER.
- Authorization is computed server-side from membership + role on every request;
  the Android UI may hide what a user can't do, but must never be the enforcement point.
- A user can hold a WorkerProfile and company memberships simultaneously; the in-app
  role-selection chooses the *experience*, not an exclusive account "type".

## Consequences
- Onboarding flows are separate per experience but converge on one `User`.
- Every domain query is scoped by caller context (worker id or company membership),
  which must be enforced in the data layer (query filters), not by convention.
- Company verification states (PENDING/VERIFIED/REJECTED/SUSPENDED/EXPIRED) live on
  `Company` and gate employer capabilities server-side.
