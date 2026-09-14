<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum PreparationQuantityMode: string
{
    case PerGuest = 'per_guest';
    case Fixed = 'fixed';
}
