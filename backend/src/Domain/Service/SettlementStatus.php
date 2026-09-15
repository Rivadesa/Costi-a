<?php

declare(strict_types=1);
namespace Hospitality\Domain\Service;

/** Derived from charges and payments, never used to drive the kitchen. */
enum SettlementStatus: string
{
    case Unpaid = 'unpaid';
    case PartiallyPaid = 'partially_paid';
    case Paid = 'paid';
    case Overpaid = 'overpaid';
}
