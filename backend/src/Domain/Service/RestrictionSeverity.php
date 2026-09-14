<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum RestrictionSeverity: string
{
    case Informative = 'informative';
    case Important = 'important';
    case Critical = 'critical';
}
