<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum ServiceStatus: string
{
    case Open = 'OPEN';
    case InService = 'IN_SERVICE';
    case Paused = 'PAUSED';
    case PendingPayment = 'PAYMENT_PENDING';
    case Paid = 'PAID';
    case Closed = 'CLOSED';
    case Cancelled = 'CANCELLED';
}
