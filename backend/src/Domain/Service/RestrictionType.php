<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum RestrictionType: string
{
    case Allergy = 'ALLERGY';
    case Intolerance = 'INTOLERANCE';
    case Preference = 'PREFERENCE';
}
