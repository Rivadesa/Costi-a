<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum CourseStatus: string
{
    case Pending = 'pending';
    case Fired = 'fired';
    case Preparing = 'preparing';
    case Ready = 'ready';
    case Served = 'served';
    case Skipped = 'skipped';
    case Cancelled = 'cancelled';
}
