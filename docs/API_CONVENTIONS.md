# API Conventions — V1A

## 1. General principles

The local Laravel API is authoritative for live operation.

Clients:
- issue commands over HTTP/API;
- query current projections/state;
- subscribe to realtime updates;
- never write directly to PostgreSQL.

## 2. Resource naming

Prefer explicit resources:
- `/api/v1/services`
- `/api/v1/services/{serviceId}`
- `/api/v1/services/{serviceId}/guests`
- `/api/v1/services/{serviceId}/courses`
- `/api/v1/kitchen/stations/{stationId}/queue`
- `/api/v1/service-board`

Avoid provider-specific names in core routes.

## 3. Commands vs generic PATCH

For state transitions with domain rules, prefer explicit command endpoints over arbitrary field mutation.

Examples:

```text
POST /services/{id}/start
POST /services/{id}/courses/{courseId}/fire
POST /services/{id}/courses/{courseId}/preparations/{itemId}/start
POST /services/{id}/courses/{courseId}/preparations/{itemId}/ready
POST /services/{id}/courses/{courseId}/ready
POST /services/{id}/courses/{courseId}/serve
POST /services/{id}/courses/{courseId}/skip
POST /services/{id}/pause
POST /services/{id}/resume
```

This keeps invariants server-side and creates clear audit semantics.

## 4. Idempotency

Retryable commands accept header:

`Idempotency-Key: <ULID/UUID>`

Rules:
- same key + same semantic request returns/replays original successful outcome;
- same key + conflicting payload should fail clearly;
- idempotency records need an appropriate retention strategy;
- clients generate key before first attempt and retain it through retry.

High-priority idempotent actions:
- course fire;
- preparation start/ready;
- course ready/serve;
- add consumption;
- cancel consumption;
- payment record;
- close service.

## 5. Error shape

Recommended JSON shape:

```json
{
  "error": {
    "code": "COURSE_NOT_READY",
    "message": "Required preparations are still pending.",
    "details": {
      "pending_preparation_ids": ["..."]
    }
  }
}
```

Domain error codes are stable for clients; human message may evolve/localize.

## 6. HTTP semantics

Suggested mapping:
- `200/201`: successful command/query;
- `400`: invalid input shape/value;
- `401`: unauthenticated;
- `403`: authenticated but unauthorized;
- `404`: resource not found in tenant/company context;
- `409`: domain state conflict/idempotency conflict;
- `422`: semantically invalid request where appropriate;
- `503`: local infrastructure dependency unavailable.

## 7. Tenant/company context

Never accept arbitrary tenant id from an untrusted client as sufficient authorization.

Tenant/company/location context should be derived/validated from authenticated session/device and route/resource access.

## 8. Realtime events

Realtime messages are hints/projections after durable state change.

Suggested envelope:

```json
{
  "event_id": "01...",
  "type": "course.ready",
  "occurred_at": "2026-09-14T20:31:45+02:00",
  "tenant_id": "...",
  "company_id": "...",
  "location_id": "...",
  "aggregate_type": "table_service",
  "aggregate_id": "...",
  "data": {
    "course_id": "...",
    "table_id": "..."
  }
}
```

Clients must fetch current API state if they suspect missed events.

## 9. Versioning

Use `/api/v1` from first public/internal contract. Breaking changes require explicit version strategy rather than silently changing clients.

Internal DTOs may evolve faster before a deployed release, but documentation/OpenAPI should match the active client branch.

## 10. Money

API monetary values use integer minor units (`*_cents`) until/unless a currency abstraction requires broader naming.

Never use floating point for money.

Include currency at account/company configuration boundary when multi-currency becomes relevant.

## 11. Time

Persist timestamps with timezone awareness (PostgreSQL `timestamptz` or normalized UTC strategy). API returns ISO-8601 timestamps.

Display uses establishment timezone.

Elapsed service timers derive from timestamps; do not increment counters as authoritative data.

## 12. Deletes

Business operations that require history use cancellation/void commands, not HTTP DELETE.

DELETE is reserved for configuration entities that are genuinely safe to remove and not referenced/audited, subject to policy.

## 13. Pagination and projections

KDS/service board endpoints are operational projections and may use purpose-built DTOs rather than exposing normalized tables.

Do not force clients to make 20 requests and join domain objects themselves during live service.

## 14. OpenAPI

Maintain OpenAPI spec alongside implementation. For every command endpoint document:
- authorization;
- idempotency behavior;
- request schema;
- response projection;
- domain conflict codes;
- emitted event(s).
