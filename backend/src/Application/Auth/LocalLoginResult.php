<?php

declare(strict_types=1);

namespace Hospitality\Application\Auth;

final readonly class LocalLoginResult
{
    public function __construct(
        public string $token,
        public \DateTimeImmutable $expiresAt,
        public LocalPrincipal $principal,
    ) {
    }
}
