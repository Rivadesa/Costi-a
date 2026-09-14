<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum CourseItemStatus: string
{
    case Pending = 'pending';
    case Fired = 'fired';
    case Preparing = 'preparing';
    case Ready = 'ready';
    case Cancelled = 'cancelled';
}
