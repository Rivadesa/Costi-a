<?php

declare(strict_types=1);

use App\Infrastructure\Persistence\LaravelIdempotencyStore;
use App\Infrastructure\Persistence\LaravelOutboxStore;
use App\Infrastructure\Persistence\LaravelTableServiceRepository;
use App\Infrastructure\Persistence\LaravelTransactionManager;
use Hospitality\Application\Contracts\OutboxStore;
use Hospitality\Application\Service\ServiceMutationExecutor;
use Hospitality\Application\Service\TableServiceSetupService;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Application\Shared\ConcurrencyConflict;
use Hospitality\Domain\Service\TableService;
use Hospitality\Domain\Shared\DomainEvent;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Contracts\Console\Kernel;
use Illuminate\Support\Facades\DB;

require __DIR__.'/../vendor/autoload.php';
$app = require __DIR__.'/../bootstrap/app.php';
$app->make(Kernel::class)->bootstrap();

function reliabilityCheck(bool $condition, string $message): void
{
    if (!$condition) {
        throw new RuntimeException('FAIL: '.$message);
    }
    echo "✓ {$message}\n";
}

$tenantId = Ulid::generate();
$companyId = Ulid::generate();
$locationId = Ulid::generate();
$areaId = Ulid::generate();
$tableId = Ulid::generate();
$now = now();

DB::table('tenants')->insert([
    'id' => $tenantId,
    'name' => 'Reliability Tenant',
    'created_at' => $now,
    'updated_at' => $now,
]);
DB::table('companies')->insert([
    'id' => $companyId,
    'tenant_id' => $tenantId,
    'legal_name' => 'Reliability Company SL',
    'trade_name' => 'Reliability',
    'tax_id' => null,
    'active' => true,
    'created_at' => $now,
    'updated_at' => $now,
]);
DB::table('locations')->insert([
    'id' => $locationId,
    'tenant_id' => $tenantId,
    'company_id' => $companyId,
    'name' => 'Reliability Restaurant',
    'timezone' => 'Europe/Madrid',
    'active' => true,
    'created_at' => $now,
    'updated_at' => $now,
]);
DB::table('dining_areas')->insert([
    'id' => $areaId,
    'tenant_id' => $tenantId,
    'company_id' => $companyId,
    'location_id' => $locationId,
    'name' => 'Sala',
    'sequence' => 1,
    'active' => true,
]);
DB::table('dining_tables')->insert([
    'id' => $tableId,
    'tenant_id' => $tenantId,
    'company_id' => $companyId,
    'location_id' => $locationId,
    'dining_area_id' => $areaId,
    'code' => 'R1',
    'name' => 'Mesa R1',
    'capacity' => 4,
    'sequence' => 1,
    'active' => true,
]);

$context = new CommandContext($tenantId, $companyId, $locationId, null, 'reliability-test');
$setup = $app->make(TableServiceSetupService::class);
$opened = $setup->open($context, 'reliability-open', $tableId, 2);
$serviceId = $opened['service_id'];

// Two independent repository instances simulate two application requests/terminals
// that read the same aggregate version before either writes.
$repoA = new LaravelTableServiceRepository();
$repoB = new LaravelTableServiceRepository();
$terminalA = $repoA->get($tenantId, $companyId, $locationId, $serviceId);
$terminalB = $repoB->get($tenantId, $companyId, $locationId, $serviceId);

$initialVersion = (int) DB::table('table_services')->where('id', $serviceId)->value('version');
$terminalA->addConsumption('Agua', 1, 500);
$repoA->save($terminalA);

$concurrencyRejected = false;
$terminalB->addConsumption('Café', 1, 300);
try {
    $repoB->save($terminalB);
} catch (ConcurrencyConflict) {
    $concurrencyRejected = true;
}

reliabilityCheck($concurrencyRejected, 'stale terminal write is rejected by optimistic locking');
reliabilityCheck(
    (int) DB::table('table_services')->where('id', $serviceId)->value('version') === $initialVersion + 1,
    'failed stale write does not advance aggregate version',
);
reliabilityCheck(
    DB::table('consumptions')->where('table_service_id', $serviceId)->where('name', 'Café')->count() === 0,
    'failed stale write does not persist child rows',
);
reliabilityCheck(
    DB::table('consumptions')->where('table_service_id', $serviceId)->where('name', 'Agua')->count() === 1,
    'winning terminal mutation remains persisted',
);

// Simulate an infrastructure failure after aggregate persistence but before the
// outbox can accept the event. Everything must roll back atomically.
$versionBeforeRollback = (int) DB::table('table_services')->where('id', $serviceId)->value('version');
$throwingOutbox = new class implements OutboxStore {
    public function append(string $tenantId, string $companyId, string $locationId, DomainEvent $event): void
    {
        throw new RuntimeException('Simulated outbox storage failure');
    }
};

$executor = new ServiceMutationExecutor(
    new LaravelTableServiceRepository(),
    new LaravelTransactionManager(),
    new LaravelIdempotencyStore(),
    $throwingOutbox,
);

$rollbackRaised = false;
try {
    $executor->execute(
        $context,
        $serviceId,
        'reliability-rollback',
        'test.rollback',
        ['name' => 'Rollback Wine'],
        static function (TableService $service): array {
            $consumption = $service->addConsumption('Rollback Wine', 1, 9999);
            return ['consumption_id' => $consumption->id];
        },
    );
} catch (RuntimeException $exception) {
    $rollbackRaised = $exception->getMessage() === 'Simulated outbox storage failure';
}

reliabilityCheck($rollbackRaised, 'simulated outbox failure reaches transaction boundary');
reliabilityCheck(
    DB::table('consumptions')->where('table_service_id', $serviceId)->where('name', 'Rollback Wine')->count() === 0,
    'domain write is rolled back when outbox write fails',
);
reliabilityCheck(
    (int) DB::table('table_services')->where('id', $serviceId)->value('version') === $versionBeforeRollback,
    'aggregate version is rolled back with failed transaction',
);
reliabilityCheck(
    DB::table('idempotency_keys')->where('tenant_id', $tenantId)->where('idempotency_key', 'reliability-rollback')->count() === 0,
    'idempotency result is not remembered for rolled-back command',
);

echo "\nAll PostgreSQL reliability checks passed.\n";
