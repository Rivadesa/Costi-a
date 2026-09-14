# ADR-005 — Separate operational service, account, payment and fiscal document

**Status:** Accepted

## Context

Fine-dining operation needs a mutable provisional account during service. Spanish fiscal rules impose different integrity/correction requirements once fiscal documents/records exist. Treating everything as a generic "ticket" would create an architectural trap.

## Decision

Keep distinct concepts:

```text
TableService
  → Account / Settlement
  → Payment
  → FiscalDocument (V1B)
  → FiscalRecord (V1B)
```

V1A implements operational service/account/payment only. V1B implements fiscal issuance and SIF/VERI*FACTU according to current law/specification.

## Consequences

- a provisional account can be adjusted while service is open without pretending it is a fiscal document;
- operational cancellation remains auditable;
- once a fiscal document exists, correction follows fiscal workflow rather than destructive delete;
- V1A can ship before replacing the existing fiscal solution while preserving the correct boundary.

## Constraint

Future V1B must revalidate legal/technical requirements against then-current official specifications before implementation/release.
