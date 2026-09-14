# Development Guide

## 1. Branching model

- `main`: stable/release cuts only.
- `develop`: integration branch for active product work.
- `feature/<short-name>`: optional for non-trivial isolated work.
- `fix/<short-name>`: bug fixes.

Pull requests should normally target `develop`. Release PRs target `main`.

## 2. Repository layout

```text
backend/
  composer.json
  src/Domain/...
  # future full Laravel application structure

frontend/
  operator/
    # Vue 3/Vite/Tauri operator app

docs/
  adr/
  *.md
```

As Laravel is materialized, preserve domain/application/infrastructure boundaries rather than placing all logic in controllers/models.

## 3. Target local stack

- PHP 8.4+
- Laravel current supported version selected for implementation
- Composer
- PostgreSQL
- Redis
- Node.js LTS/current supported
- npm/pnpm (choose and document once lockfile is committed)
- Rust toolchain + Tauri prerequisites for desktop build

## 4. Environment roles

### Local development
Developer machine may run all services locally or via containers.

### Restaurant local server
Production-like local authority:
- Laravel/API;
- PostgreSQL;
- Redis/worker/realtime;
- backup agent;
- monitored as infrastructure.

### Operator client
Tauri Windows application or PWA depending device.

## 5. Coding rules — backend

- `declare(strict_types=1);`
- domain state transitions through methods, not public arbitrary field mutation where avoidable;
- money as integer minor units;
- timestamps as immutable/timezone-aware values;
- stable global IDs;
- domain exceptions mapped to stable API error codes;
- no external API calls inside domain transaction critical path;
- critical writes + outbox in same DB transaction;
- authorization server-side.

## 6. Coding rules — frontend

- business invariants stay on backend;
- frontend may derive display state/timers;
- command functions always handle pending/success/failure explicitly;
- retryable commands keep idempotency key across uncertain retries;
- critical allergy display cannot rely only on CSS color;
- live-service navigation should minimize taps;
- realtime messages trigger projection updates but API refresh remains fallback.

## 7. Adding a new feature

Before coding:
1. identify phase (V1A/V1B/...);
2. verify feature belongs in current scope;
3. identify aggregate/invariants;
4. update ADR if architecture changes;
5. write/adjust acceptance scenario.

During coding:
1. domain/application tests first for non-trivial rule;
2. implement persistence/API;
3. implement UI;
4. verify idempotency/retry implications;
5. update docs/status.

After coding:
1. run automated tests/lint/build;
2. update `docs/STATUS.md`;
3. update OpenAPI when contract changes;
4. link issue/PR to decision docs when relevant.

## 8. Commit style

Prefer Conventional Commit-style messages:
- `feat:`
- `fix:`
- `docs:`
- `test:`
- `refactor:`
- `chore:`
- `build:`

Commits should explain one coherent change when practical.

## 9. Pull request checklist

A PR should state:
- problem/use case;
- scope;
- screenshots for UI changes when possible;
- domain/API changes;
- tests performed;
- migration impact;
- offline/retry impact;
- documentation updated;
- known limitations.

## 10. AI collaborator protocol

An AI joining the repository should read:
1. `README.md`;
2. `docs/AI_CONTEXT.md`;
3. `docs/STATUS.md`;
4. `docs/DOMAIN_MODEL.md`;
5. relevant ADRs.

Before making large architectural changes, it should compare proposal against existing ADRs and either preserve them or create a superseding ADR. It must not silently reinterpret product scope.

## 11. Secrets

Never commit:
- `.env` with credentials;
- database dumps containing personal data;
- API tokens;
- WooCommerce secrets;
- GitHub tokens;
- certificates/private keys.

Commit `.env.example` with documented placeholders.

## 12. Migrations and seed data

Provide deterministic development seed fixtures representing Retiro-like flows without real customer personal data:
- 8 example tables;
- sample menu with multiple courses;
- several kitchen stations;
- example guests/restrictions;
- example consumptions.

Seeds are development/demo data, not production bootstrap assumptions.

## 13. Observability

From early V1A retain useful timestamps and event IDs. Later dashboards require source data such as:
- course fired time;
- preparation started/ready;
- course ready;
- served;
- service opened/closed.

Do not add manual UI interactions solely to generate metrics that can be derived from timestamps already captured.
