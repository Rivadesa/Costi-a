<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

use Hospitality\Application\Contracts\IdempotencyStore;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Contracts\TableServiceRepository;
use Hospitality\Application\Contracts\TransactionManager;
use Hospitality\Application\Service\ServiceMutationExecutor;
use Hospitality\Application\Service\TableServiceCommandService;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\IdempotencyConflict;
use Hospitality\Application\Shared\IdempotencyRecord;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\PreparationTemplate;
use Hospitality\Domain\Service\TableService;
use Hospitality\Domain\Shared\DomainEvent;

final class MemoryServiceRepository implements TableServiceRepository
{
    /** @var array<string, TableService> */
    public array $services = [];
    public int $saveCount = 0;

    public function get(string $tenantId, string $companyId, string $locationId, string $serviceId): TableService
    {
        $service = $this->services[$serviceId] ?? throw new RuntimeException('not found');
        if ($service->tenantId !== $tenantId || $service->companyId !== $companyId || $service->locationId !== $locationId) {
            throw new RuntimeException('context mismatch');
        }
        return $service;
    }

    public function save(TableService $service): void
    {
        $this->services[$service->id] = $service;
        $this->saveCount++;
    }
}

final class MemoryTransactions implements TransactionManager
{
    public int $runs = 0;
    public function run(callable $callback): mixed
    {
        $this->runs++;
        return $callback();
    }
}

final class MemoryIdempotency implements IdempotencyStore
{
    /** @var array<string, IdempotencyRecord> */
    public array $records = [];

    public function find(string $tenantId, string $idempotencyKey): ?IdempotencyRecord
    {
        return $this->records[$tenantId . ':' . $idempotencyKey] ?? null;
    }

    public function remember(string $tenantId, string $idempotencyKey, string $commandName, string $requestHash, array $result): void
    {
        $this->records[$tenantId . ':' . $idempotencyKey] = new IdempotencyRecord($tenantId, $idempotencyKey, $commandName, $requestHash, $result);
    }
}

final class MemoryOutbox implements OutboxStore
{
    /** @var list<DomainEvent> */
    public array $events = [];

    public function append(string $tenantId, string $companyId, string $locationId, DomainEvent $event): void
    {
        $this->events[] = $event;
    }
}

$tenant = 'tenant-1';
$company = 'company-1';
$location = 'location-1';
$service = TableService::open('service-1', $tenant, $company, $location, 'table-4', 2, new DateTimeImmutable());
$menu = new MenuTemplate('menu-1', 'Experience', 15000, [
    new CourseTemplate('course-1', 1, 'Starter', [new PreparationTemplate('prep-1', 'Starter prep', 'cold')]),
]);
$service->assignMenu($menu);
$service->pullEvents(); // Setup is assumed persisted before command-service tests.

$repo = new MemoryServiceRepository();
$repo->services[$service->id] = $service;
$transactions = new MemoryTransactions();
$idempotency = new MemoryIdempotency();
$outbox = new MemoryOutbox();
$executor = new ServiceMutationExecutor($repo, $transactions, $idempotency, $outbox);
$commands = new TableServiceCommandService($executor);
$context = new CommandContext($tenant, $company, $location, 'user-1', 'device-1');

$first = $commands->start($context, $service->id, 'key-start-1');
$second = $commands->start($context, $service->id, 'key-start-1');
ok($first === $second, 'idempotent retry returns the original command result');
ok($repo->saveCount === 1, 'idempotent retry does not persist the mutation twice');
ok(count($outbox->events) === 1 && $outbox->events[0]->type === 'service.started', 'idempotent retry writes one outbox event');

$course = $commands->fireNextCourse($context, $service->id, 'key-fire-1');
$fireAgain = $commands->fireNextCourse($context, $service->id, 'key-fire-1');
ok($course === $fireAgain, 'course fire can be safely retried after uncertain client response');
ok(count(array_filter($outbox->events, fn (DomainEvent $event): bool => $event->type === 'course.fired')) === 1, 'course is fired into outbox only once');

throws(
    fn () => $commands->start($context, $service->id, 'key-fire-1'),
    'reusing an idempotency key for a different command is rejected',
);

try {
    $commands->addConsumption($context, $service->id, 'key-consumption', 'Water', 1, 500);
    $commands->addConsumption($context, $service->id, 'key-consumption', 'Wine', 1, 4200);
    throw new RuntimeException('expected idempotency conflict');
} catch (IdempotencyConflict) {
    echo "✓ reusing an idempotency key with different payload is rejected\n";
}

ok(count($idempotency->records) === 3, 'successful command results are remembered for retries');

echo "\nAll V1A application-layer tests passed.\n";
