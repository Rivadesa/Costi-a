<?php

declare(strict_types=1);

namespace Hospitality\Application\Contracts;

use Hospitality\Application\Auth\LocalLoginResult;
use Hospitality\Application\Auth\LocalPrincipal;

interface LocalAuthGateway
{
    public function login(
        string $tenantId,
        string $email,
        string $password,
        string $companyId,
        string $locationId,
        string $deviceName,
    ): LocalLoginResult;

    public function authenticate(
        string $rawToken,
        string $companyId,
        string $locationId,
    ): LocalPrincipal;

    public function revoke(string $rawToken): void;
}
