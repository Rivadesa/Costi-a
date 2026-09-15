<?php

declare(strict_types=1);
namespace Hospitality\Domain\Service;

/** Occupation by this party, not the table's future cleaning workflow. */
enum OccupancyStatus: string
{
    case Occupied = 'occupied';
    case Released = 'released';
}
