# Security Baseline

## Scope

Security requirements will evolve, but V1A must not accumulate shortcuts that later make restaurant deployment unsafe.

## Identity and authorization

- Every user action reaching the API is authenticated once authentication is enabled.
- Authorization is server-side; UI visibility is not a security boundary.
- Roles/permissions are scoped to tenant/company/location as appropriate.
- Critical actions (service cancellation, payment changes, configuration changes) require explicit permissions.

## Device/client trust

Tauri/PWA clients are untrusted from a backend perspective. Never assume a packaged desktop app cannot be modified.

## Secrets

Never commit:
- database passwords;
- API tokens;
- WooCommerce consumer secrets;
- private certificates/keys;
- production `.env`;
- real customer exports.

Use environment/configuration secret storage appropriate to local server deployment.

## Network

- Local API should bind/configure only as needed for trusted LAN clients.
- Production should use TLS where practical/required, especially cloud traffic.
- Cloud sync/integrations always use encrypted transport.
- Do not expose PostgreSQL directly to client devices.

## Data protection

Customer/restriction data may be personal/sensitive operational information. Apply least-privilege access and avoid copying full customer records into KDS when only restriction/preparation context is needed.

## Audit

Security-relevant events should be auditable:
- login failures/privilege changes (when auth implemented);
- service cancellation;
- consumption voids;
- payment corrections;
- administrative configuration changes.

Audit logs must not contain secrets.

## API

- Validate all input server-side.
- Protect against tenant/company IDOR by scoping queries to authorized context.
- Rate-limit authentication/external endpoints where relevant.
- Use idempotency to prevent duplicate financial/operational mutations, not as authentication.

## Dependencies

- Commit lockfiles once actual Laravel/Node/Tauri apps are materialized.
- Enable dependency/security scanning in CI.
- Patch supported versions promptly, especially local-server exposed components.

## Backup security

Backups containing business/customer data must be encrypted in transit and at rest and have documented access/retention.

## Future fiscal/payment modules

Payment card data should not be stored unless a specific compliant integration requires it; prefer token/reference from certified payment provider/terminal.

Fiscal certificates/keys require dedicated secret handling and should never be distributed to ordinary client workstations.
