<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

use Hospitality\Domain\Shared\Ulid;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\PreparationQuantityMode;
use Hospitality\Domain\Service\PreparationTemplate;
use Hospitality\Domain\Service\Restriction;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Service\ServiceStatus;
use Hospitality\Domain\Service\TableService;

$stationCold = Ulid::generate();
$stationFish = Ulid::generate();
$stationMeat = Ulid::generate();
$stationPass = Ulid::generate();

$menu = new MenuTemplate(
    Ulid::generate(),
    'Menú Experiencia',
    15000,
    [
        new CourseTemplate(Ulid::generate(), 1, 'Snacks', [
            new PreparationTemplate(Ulid::generate(), 'Snack', $stationCold, PreparationQuantityMode::PerGuest),
        ]),
        new CourseTemplate(Ulid::generate(), 2, 'Pescado', [
            new PreparationTemplate(Ulid::generate(), 'Rodaballo', $stationFish, PreparationQuantityMode::PerGuest),
            new PreparationTemplate(Ulid::generate(), 'Salsa', $stationPass, PreparationQuantityMode::Fixed, 1),
        ]),
        new CourseTemplate(Ulid::generate(), 3, 'Carne', [
            new PreparationTemplate(Ulid::generate(), 'Carne', $stationMeat, PreparationQuantityMode::PerGuest),
        ]),
    ],
);

$service = TableService::open(
    Ulid::generate(),
    Ulid::generate(),
    Ulid::generate(),
    Ulid::generate(),
    'mesa-4',
    2,
    new DateTimeImmutable(),
);
$service->assignMenu($menu);
$service->addGuest('Ana');
$g2 = $service->addGuest('Carlos');
$service->addRestriction($g2->id, new Restriction(Ulid::generate(), 'Marisco', RestrictionType::Allergy, RestrictionSeverity::Critical));
$service->start();
ok($service->status === ServiceStatus::InService, 'service starts after menu assignment');
ok(count($service->guests()) === 2, 'service keeps per-guest identity');
ok(count($service->guests()[1]->restrictions()) === 1, 'critical restriction is attached to specific guest');

$c1 = $service->fireNextCourse();
ok(count($c1->items()) === 2, 'per-guest preparation expands into one kitchen item per guest');
foreach ($c1->items() as $item) {
    $service->startCourseItem($c1->id, $item->id);
    $service->markCourseItemReady($c1->id, $item->id);
}
$service->markCourseReady($c1->id);
$service->serveCourse($c1->id);
ok($c1->status->value === 'served', 'course follows fired → station preparation → chef ready → served');

$c2 = $service->fireNextCourse();
ok(count($c2->items()) === 3, 'fixed and per-guest preparations coexist in one course');
throws(fn () => $service->markCourseReady($c2->id), 'chef cannot validate course until mandatory station items are ready');
throws(fn () => $service->fireNextCourse(), 'next course cannot fire while current one is active');
$service->skipCourse($c2->id, 'Adaptación decidida por chef');
ok($c2->status->value === 'skipped', 'course can be skipped with traceable reason');

$c3 = $service->fireNextCourse();
foreach ($c3->items() as $item) {
    $service->startCourseItem($c3->id, $item->id);
    $service->markCourseItemReady($c3->id, $item->id);
}
$service->markCourseReady($c3->id);
$service->serveCourse($c3->id);

$extra = $service->addExtraCourse('Atención de cocina', [
    new PreparationTemplate(Ulid::generate(), 'Petit extra', $stationPass, PreparationQuantityMode::Fixed, 1),
]);
$extra->fire();
foreach ($extra->items() as $item) {
    $extra->startItem($item->id);
    $extra->markItemReady($item->id);
}
$extra->validateReady();
$extra->serve();
ok($extra->extra, 'chef can add an extra course without changing menu template');

$service->addConsumption('Muga Reserva 2021', 1, 4200);
$water = $service->addConsumption('Agua con gas', 2, 500);
ok($service->subtotalCents() === 35200, 'provisional account combines menu and extra consumptions');
$service->cancelConsumption($water->id, 'Duplicado');
ok($service->subtotalCents() === 34200, 'cancelled consumption remains traceable but leaves total');

$service->recordPayment('card', 34200);
ok($service->status === ServiceStatus::InService, 'full payment does not change operational status');
$service->close();
ok($service->status === ServiceStatus::Closed, 'paid service closes operationally');

$events = $service->pullEvents();
ok(count($events) >= 20, 'domain emits events for relevant operations');
ok(count(array_filter($events, fn ($e) => $e->type === 'course.fired')) === 3, 'course fire events are emitted');
ok(count(array_filter($events, fn ($e) => $e->type === 'course_item.ready')) >= 4, 'station-level preparation events are emitted');

$restored = TableService::reconstitute(
    $service->id,
    $service->tenantId,
    $service->companyId,
    $service->locationId,
    $service->tableId,
    $service->pax,
    $service->openedAt,
    $service->status,
    $service->menu(),
    $service->guests(),
    $service->courses(),
    $service->consumptions(),
    $service->payments(),
);
ok($restored->pullEvents() === [], 'rehydration emits no fake domain events');

echo "\nAll V1A domain smoke tests passed.\n";
