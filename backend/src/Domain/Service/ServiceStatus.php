<?php

declare(strict_types=1);

namespace Hospitality\Domain\Service;

enum ServiceStatus: string
{
    case Prepared = 'prepared';
    case Open = 'open';
    case InService = 'in_service';
    case Paused = 'paused';
    // Legacy read/migration detection only. Never persisted by the D0 runtime.
    case PendingPayment = 'pending_payment';
    case Paid = 'paid';
    case Closed = 'closed';
    case Cancelled = 'cancelled';
}
