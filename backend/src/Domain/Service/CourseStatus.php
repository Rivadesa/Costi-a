<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum CourseStatus: string
{
    case Pending = 'PENDING';
    case Fired = 'FIRED';
    case Preparing = 'PREPARING';
    case Ready = 'READY';
    case Served = 'SERVED';
    case Skipped = 'SKIPPED';
    case Cancelled = 'CANCELLED';
}
