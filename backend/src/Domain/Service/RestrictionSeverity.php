<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum RestrictionSeverity: string
{
    case Informative = 'INFORMATIVE';
    case Important = 'IMPORTANT';
    case Critical = 'CRITICAL';
}
