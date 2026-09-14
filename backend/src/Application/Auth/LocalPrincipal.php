<?php

declare(strict_types=1);

namespace Hospitality\Application\Auth;

final readonly class LocalPrincipal
{
    /** @param list<string> $permissions */
    public function __construct(
        public string $userId,
        public string $tenantId,
        public string $companyId,
        public string $locationId,
        public string $displayName,
        public array $permissions,
        public ?string $tokenId = null,
    ) {
    }

    public function can(string $permission): bool
    {
        return in_array('*', $this->permissions, true)
            || in_array($permission, $this->permissions, true);
    }

    /** @return array<string, mixed> */
    public function toArray(): array
    {
        return [
            'user_id' => $this->userId,
            'tenant_id' => $this->tenantId,
            'company_id' => $this->companyId,
            'location_id' => $this->locationId,
            'display_name' => $this->displayName,
            'permissions' => $this->permissions,
        ];
    }
}
