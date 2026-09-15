<?php

declare(strict_types=1);

use Hospitality\Application\Kitchen\KdsProjector;
use Hospitality\Application\Service\TableServiceCommandService;
use Hospitality\Application\Service\TableServiceQueryService;
use Hospitality\Application\Service\TableServiceSetupService;
use Hospitality\Application\ServiceBoard\OperationalReadService;
use Hospitality\Application\Shared\CommandContext;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Contracts\Console\Kernel;
use Illuminate\Support\Facades\DB;

require __DIR__.'/../vendor/autoload.php';
$app = require __DIR__.'/../bootstrap/app.php';
$app->make(Kernel::class)->bootstrap();

function check(bool $condition, string $message): void
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
$fishStationId = Ulid::generate();
$passStationId = Ulid::generate();
$menuId = Ulid::generate();
$courseTemplateId = Ulid::generate();
$fishPreparationId = Ulid::generate();
$garnishPreparationId = Ulid::generate();
$now = now();

DB::table('tenants')->insert([
    'id' => $tenantId, 'name' => 'Integration Tenant', 'created_at' => $now, 'updated_at' => $now,
]);
DB::table('companies')->insert([
    'id' => $companyId, 'tenant_id' => $tenantId, 'legal_name' => 'Integration Company SL',
    'trade_name' => 'Integration Restaurant', 'tax_id' => 'B00000000', 'active' => true,
    'created_at' => $now, 'updated_at' => $now,
]);
DB::table('locations')->insert([
    'id' => $locationId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
    'name' => 'Main Restaurant', 'timezone' => 'Europe/Madrid', 'active' => true,
    'created_at' => $now, 'updated_at' => $now,
]);
DB::table('dining_areas')->insert([
    'id' => $areaId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
    'location_id' => $locationId, 'name' => 'Sala', 'sequence' => 1, 'active' => true,
]);
DB::table('dining_tables')->insert([
    'id' => $tableId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
    'location_id' => $locationId, 'dining_area_id' => $areaId, 'code' => 'M1',
    'name' => 'Mesa 1', 'capacity' => 4, 'sequence' => 1, 'active' => true,
]);
DB::table('kitchen_stations')->insert([
    [
        'id' => $fishStationId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
        'location_id' => $locationId, 'name' => 'Pescados', 'sequence' => 1, 'active' => true,
    ],
    [
        'id' => $passStationId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
        'location_id' => $locationId, 'name' => 'Pase', 'sequence' => 2, 'active' => true,
    ],
]);
DB::table('menu_templates')->insert([
    'id' => $menuId, 'tenant_id' => $tenantId, 'company_id' => $companyId,
    'location_id' => $locationId, 'name' => 'Menú Experiencia', 'price_cents' => 10000,
    'currency' => 'EUR', 'active' => true, 'version' => 1,
    'created_at' => $now, 'updated_at' => $now,
]);
DB::table('course_templates')->insert([
    'id' => $courseTemplateId, 'menu_template_id' => $menuId, 'sequence' => 1,
    'name' => 'Pescado', 'active' => true,
]);
DB::table('preparation_templates')->insert([
    [
        'id' => $fishPreparationId, 'course_template_id' => $courseTemplateId,
        'station_id' => $fishStationId, 'name' => 'Rodaballo', 'quantity_mode' => 'per_guest',
        'fixed_quantity' => 1, 'mandatory' => true, 'sequence' => 1,
    ],
    [
        'id' => $garnishPreparationId, 'course_template_id' => $courseTemplateId,
        'station_id' => $passStationId, 'name' => 'Guarnición', 'quantity_mode' => 'fixed',
        'fixed_quantity' => 1, 'mandatory' => true, 'sequence' => 2,
    ],
]);

$context = new CommandContext($tenantId, $companyId, $locationId, null, 'integration-test');
$setup = $app->make(TableServiceSetupService::class);
$commands = $app->make(TableServiceCommandService::class);
$queries = $app->make(TableServiceQueryService::class);
$operational = $app->make(OperationalReadService::class);

$opened = $setup->open($context, 'integration-open', $tableId, 2);
$serviceId = $opened['service_id'];
check($opened['status'] === 'open', 'table service opens against PostgreSQL');

$setup->assignMenu($context, $serviceId, 'integration-menu', $menuId);
$guest1 = $commands->addGuest($context, $serviceId, 'integration-guest-1', 'Ana');
$guest2 = $commands->addGuest($context, $serviceId, 'integration-guest-2', 'Carlos');
$commands->addRestriction(
    $context,
    $serviceId,
    'integration-restriction',
    $guest2['guest_id'],
    'Marisco',
    RestrictionType::Allergy,
    RestrictionSeverity::Critical,
    'Adaptación confirmada',
);
$commands->start($context, $serviceId, 'integration-start');
$fired = $commands->fireNextCourse($context, $serviceId, 'integration-fire');
$courseId = $fired['course_id'];
check(count($fired['items']) === 3, 'per-guest and fixed preparations expand into three KDS items');

$detail = $queries->detail($context, $serviceId);
$items = $detail['courses'][0]['items'];
foreach ($items as $index => $item) {
    $commands->startPreparation($context, $serviceId, 'integration-item-start-'.$index, $courseId, $item['id']);
    $commands->markPreparationReady($context, $serviceId, 'integration-item-ready-'.$index, $courseId, $item['id']);
}

$fishQueue = $operational->stationQueue($context, $fishStationId);
check(count($fishQueue) === 1, 'KDS station sees the active service');
$criticalFound = false;
foreach ($fishQueue[0]['items'] as $item) {
    if (($item['guest_position'] ?? null) !== 2) {
        continue;
    }
    foreach ($item['restrictions'] as $restriction) {
        if ($restriction['label'] === 'Marisco' && $restriction['severity'] === 'critical') {
            $criticalFound = true;
        }
    }
}
check($criticalFound, 'critical allergy reaches only the affected guest KDS item');

$commands->validateCourseReady($context, $serviceId, 'integration-course-ready', $courseId);
$commands->serveCourse($context, $serviceId, 'integration-course-serve', $courseId);

$firstWine = $commands->addConsumption($context, $serviceId, 'integration-wine', 'Muga Reserva', 1, 4200);
$retryWine = $commands->addConsumption($context, $serviceId, 'integration-wine', 'Muga Reserva', 1, 4200);
check($firstWine === $retryWine, 'retry with same idempotency key returns original result');
check(DB::table('consumptions')->where('table_service_id', $serviceId)->count() === 1, 'idempotent retry does not duplicate consumption');

$detail = $queries->detail($context, $serviceId);
check($detail['subtotal_cents'] === 24200, 'provisional account combines menu and beverage');
$commands->recordPayment($context, $serviceId, 'integration-payment', 'CARD', 24200);
$commands->close($context, $serviceId, 'integration-close');

$closed = $queries->detail($context, $serviceId);
check($closed['status'] === 'closed', 'fully paid service closes operationally');
check(count($operational->serviceBoard($context)) === 1, 'completed service stays visible until physical release');
$commands->releaseTable($context, $serviceId, 'integration-release', 'Guests have left');
check($operational->serviceBoard($context) === [], 'released service disappears from occupied service board');
$commands->closeAccount($context, $serviceId, 'integration-account-close');
check(DB::table('idempotency_keys')->where('tenant_id', $tenantId)->where('idempotency_key', 'integration-wine')->count() === 1, 'idempotency record is stored once');
check(DB::table('outbox_events')->where('aggregate_id', $serviceId)->count() >= 10, 'domain operations are durably written to outbox');
check((int) DB::table('table_services')->where('id', $serviceId)->value('version') > 5, 'aggregate optimistic version advances across commands');

echo "\nAll PostgreSQL/Laravel V1A integration checks passed.\n";
