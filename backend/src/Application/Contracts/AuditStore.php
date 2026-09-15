<?php

declare(strict_types=1);
namespace Hospitality\Application\Contracts;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Domain\Shared\DomainEvent;
interface AuditStore
{
    public function append(CommandContext $context, DomainEvent $event): void;
}
