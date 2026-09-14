# ADR-006 — Menu snapshots and independent preparation state

**Status:** Accepted

## Context

A fine-dining menu is reusable configuration, but real service contains per-table exceptions. One course can require several kitchen stations and guest-specific adaptations.

## Decision

- Menu template is reusable master configuration.
- Assigning menu to a service creates/uses a service-specific execution snapshot.
- Exceptions mutate only that service execution.
- Each kitchen preparation has independent state and station routing.
- A course cannot become READY while required preparations remain not ready.
- Chef/pass validation may be configured as manual or automatic once prerequisites are met.

## Consequences

- historic service does not change when menu master is edited later;
- one allergy adaptation does not corrupt the whole menu template;
- multi-station readiness is modeled correctly;
- KDS can project work per station while global board projects course/table state.

## Rejected approach

A single course status changed directly by any kitchen station. That model incorrectly marks a multi-station course ready when only one component is complete.
