<?php

declare(strict_types=1);
require __DIR__.'/bootstrap.php';
use Hospitality\Domain\Service\{TableService, ServiceStatus, OccupancyStatus, SettlementStatus, MenuTemplate, CourseTemplate, PreparationTemplate, PreparationQuantityMode};
use Hospitality\Application\ServiceBoard\TableServiceDetailProjector;
use Hospitality\Application\Checkout\CheckoutDetailProjector;

function lifecycleService(): TableService {
    $s = TableService::open('s', 't', 'c', 'l', 'm', 1, new DateTimeImmutable());
    $s->assignMenu(new MenuTemplate('menu', 'Experience', 10000, [
        new CourseTemplate('c1', 1, 'First', [new PreparationTemplate('p1', 'Dish', 'fish', PreparationQuantityMode::PerGuest)]),
        new CourseTemplate('c2', 2, 'Second', []),
    ]));
    return $s;
}
$s = lifecycleService();
$s->recordPayment('card', 2000);
ok($s->status === ServiceStatus::Open && $s->settlementStatus() === SettlementStatus::PartiallyPaid, 'prepayment before start leaves service open');
$s->start(); $course = $s->fireNextCourse();
$s->recordPayment('card', 8000);
ok($s->status === ServiceStatus::InService && $s->settlementStatus() === SettlementStatus::Paid, 'full payment during course does not stop kitchen');
$s->pause();
ok($s->paidCents() === 10000, 'pause does not change payments');
throws(fn () => $s->fireNextCourse(), 'paused service cannot fire another course');
foreach ($course->items() as $item) { $s->startCourseItem($course->id, $item->id); $s->markCourseItemReady($course->id, $item->id); }
$s->markCourseReady($course->id); $s->serveCourse($course->id);
ok($course->status->value === 'served', 'already fired work can finish while pacing is paused');
$s->resume(); $second = $s->fireNextCourse();
ok($second->sequence === 2, 'subsequent course fires after full prepayment');
$s->addConsumption('Wine glass', 1, 800);
ok($s->subtotalCents() - $s->paidCents() === 800 && $s->status === ServiceStatus::InService, 'extra consumption affects balance only');
throws(fn () => $s->close(), 'service cannot complete with active kitchen work');
throws(fn () => $s->releaseTable('left'), 'table release requires operational completion');
$s->markCourseReady($second->id); $s->serveCourse($second->id);
$s->close();
ok($s->occupancy === OccupancyStatus::Occupied && $s->accountClosedAt === null, 'completion neither releases table nor closes account');
$s->releaseTable('Guests left');
ok($s->occupancy === OccupancyStatus::Released && $s->paidCents() === 10000, 'release retains unpaid account and previous payments');
throws(fn () => $s->closeAccount(), 'outstanding balance prevents financial closure');
$s->recordPayment('cash', 800); $s->closeAccount();
ok($s->status === ServiceStatus::Closed && $s->occupancy === OccupancyStatus::Released, 'settlement of released service preserves pacing and occupancy');
throws(fn () => $s->addConsumption('extra', 1, 500), 'closed account requires explicit reopening');
throws(fn () => $s->reopenAccount(' '), 'reopening needs non-empty reason');
$s->reopenAccount('Missed water'); $s->addConsumption('water', 1, 500);
ok(count($s->payments()) === 3, 'reopening never deletes payments');
$s->recordPayment('cash', 600);
ok($s->settlementStatus() === SettlementStatus::Overpaid, 'overpayment is distinguished from exact settlement');
throws(fn () => $s->closeAccount(), 'excess payment cannot be silently settled');
$financial = (new CheckoutDetailProjector())->project($s);
ok($financial['balance_cents'] === -100, 'negative balance is not hidden by zero clipping');
$ops = (new TableServiceDetailProjector())->project($s);
foreach (['payments','paid_cents','subtotal_cents','balance_cents','account_closed_at','settlement_status'] as $field) {
    ok(!array_key_exists($field,$ops), 'operational projection excludes '.$field);
}
$early = lifecycleService(); $early->recordPayment('card',10000); $early->closeAccount(); $early->start();
ok($early->fireNextCourse()->sequence === 1, 'explicit early account closure still permits kitchen service');
$unpaid = lifecycleService();
throws(fn () => $unpaid->close(), 'pending courses must be explicitly skipped, never silently lost');
$source = lifecycleService();
$legacy = TableService::reconstitute($source->id,$source->tenantId,$source->companyId,$source->locationId,$source->tableId,1,$source->openedAt,
    ServiceStatus::Paused,$source->menu(),[], $source->courses(),[],[], OccupancyStatus::Occupied,null,null,true);
throws(fn () => $legacy->resume(), 'ambiguous legacy state blocks guessed automatic resumption');
$legacy->reconcileLifecycle(ServiceStatus::Open,'Confirmed not started'); $legacy->start();
ok(!$legacy->lifecycleReviewRequired && $legacy->status === ServiceStatus::InService, 'reasoned lifecycle review permits valid operation');
$events = array_column(array_map(fn ($e) => ['type'=>$e->type],$s->pullEvents()),'type');
ok(in_array('service.completed',$events,true) && in_array('table.released',$events,true) && in_array('account.closed',$events,true), 'completion release and settlement emit distinct events');
echo "\nD0 lifecycle checks passed.\n";
