<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum CourseItemStatus: string
{
    case Pending = 'PENDING';
    case Started = 'STARTED';
    case Ready = 'READY';
}
