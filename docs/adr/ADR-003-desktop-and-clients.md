# ADR-003 — Desktop and client strategy

**Status:** Accepted

## Context

The restaurant requires a real desktop application on Windows while waiter/kitchen devices may be tablets or kiosk screens. We want one UI technology family without installing a full backend on every workstation.

## Decision

- Vue 3 + Vite for operational UI.
- Tauri 2 packages the Windows desktop application.
- PWA deployment is allowed for waiter/KDS devices where hardware/operations favor it.
- All clients communicate with the local Laravel API; they are not database authorities.

## Consequences

- A broken workstation can be replaced without restoring business DB from that workstation.
- UI code/contracts can be shared across desktop/PWA surfaces.
- Tauri-specific native integration remains at edge layer.
- Rust/Tauri toolchain is required for desktop build/release.

## Rejected alternatives

- Classic monolithic Windows app containing its own authoritative local DB on each station.
- Electron as default packaging: acceptable alternative if Tauri creates concrete blocking issues, but not chosen initially due to footprint/runtime duplication.
- Browser-only desktop requirement: rejected because product requires installed desktop experience and future native hardware/update hooks.
