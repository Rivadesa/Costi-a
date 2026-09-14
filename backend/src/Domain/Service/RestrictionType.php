<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum RestrictionType: string
{
    case Allergy = 'allergy';
    case Intolerance = 'intolerance';
    case Preference = 'preference';
}
