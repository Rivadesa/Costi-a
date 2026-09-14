<?php

declare(strict_types=1);

namespace Hospitality\Tests\Domain\Service;

use Hospitality\Domain\Service\CourseStatus;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\PreparationQuantityMode;
use Hospitality\Domain\Service\PreparationTemplate;
use Hospitality\Domain\Service\Restriction;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Service\ServiceStatus;
use Hospitality\Domain\Service\TableService;
use PHPUnit\Framework\TestCase;

final class TableServiceTest extends TestCase
{
    public function test_multi_station_course_cannot_be_ready_until_every_required_preparation_is_ready(): void
    {
        $service = $this->service(2);
        $service->assignMenu($this->menu());
        $service->start();

        $course = $service->fireNextCourse();
        self::assertSame(CourseStatus::Fired, $course->status);
        self::assertCount(3, $course->items());

        [$fishPax1, $fishPax2, $garnish] = $course->items();
        $service->startCourseItem($course->id, $fishPax1->id);
        $service->markCourseItemReady($course->id, $fishPax1->id);

        try {
            $service->markCourseReady($course->id);
            self::fail('Course readiness should be rejected while required preparations are pending.');
        } catch (\DomainException $exception) {
            self::assertSame('All mandatory preparations must be ready before validating the course.', $exception->getMessage());
        }

        foreach ([$fishPax2, $garnish] as $item) {
            $service->startCourseItem($course->id, $item->id);
            $service->markCourseItemReady($course->id, $item->id);
        }
        $service->markCourseReady($course->id);

        self::assertSame(CourseStatus::Ready, $course->status);
    }

    public function test_next_course_cannot_fire_until_current_course_is_served(): void
    {
        $service = $this->service(2);
        $service->assignMenu($this->menu());
        $service->start();
        $service->fireNextCourse();

        $this->expectException(\DomainException::class);
        $this->expectExceptionMessage('Cannot fire the next course while the current course is unfinished.');
        $service->fireNextCourse();
    }

    public function test_guest_restrictions_are_structured_and_resolved_by_guest_position(): void
    {
        $service = $this->service(2);
        $guest1 = $service->addGuest('Ana');
        $guest2 = $service->addGuest('Carlos');

        $restriction = new Restriction(
            'restriction-1',
            'Marisco',
            RestrictionType::Allergy,
            RestrictionSeverity::Critical,
        );
        $service->addRestriction($guest2->id, $restriction);

        self::assertSame([], $service->restrictionsForGuestPosition($guest1->position));
        self::assertSame([$restriction], $service->restrictionsForGuestPosition($guest2->position));
        self::assertTrue($guest2->hasCriticalRestriction());
    }

    public function test_service_cannot_have_more_guest_positions_than_pax(): void
    {
        $service = $this->service(1);
        $service->addGuest();

        $this->expectException(\DomainException::class);
        $this->expectExceptionMessage('Cannot add more guests than service pax.');
        $service->addGuest();
    }

    public function test_cancelled_consumption_is_excluded_from_subtotal_but_remains_in_history(): void
    {
        $service = $this->service(2);
        $service->assignMenu($this->menu());
        $wine = $service->addConsumption('Wine', 1, 4200);

        self::assertSame(34200, $service->subtotalCents());
        $service->cancelConsumption($wine->id, 'Added by mistake');

        self::assertSame(30000, $service->subtotalCents());
        self::assertCount(1, $service->consumptions());
        self::assertTrue($service->consumptions()[0]->isCancelled());
    }

    public function test_extra_course_changes_service_execution_not_menu_template(): void
    {
        $service = $this->service(2);
        $menu = $this->menu();
        $service->assignMenu($menu);

        $service->addExtraCourse('Chef extra');

        self::assertCount(2, $menu->courses);
        self::assertCount(3, $service->courses());
        self::assertTrue($service->courses()[2]->extra);
    }

    public function test_paid_service_can_close_when_no_course_is_active(): void
    {
        $service = $this->service(1);
        $service->assignMenu($this->singleCourseMenuWithoutKitchenWork());
        $service->start();

        $course = $service->fireNextCourse();
        $service->markCourseReady($course->id);
        $service->serveCourse($course->id);
        $service->recordPayment('CARD', 15000);
        $service->close();

        self::assertSame(ServiceStatus::Closed, $service->status);
    }

    public function test_reconstitution_does_not_emit_creation_or_other_fake_events(): void
    {
        $source = $this->service(2);
        $source->assignMenu($this->menu());
        $source->addGuest('Ana');
        $source->pullEvents();

        $restored = TableService::reconstitute(
            $source->id,
            $source->tenantId,
            $source->companyId,
            $source->locationId,
            $source->tableId,
            $source->pax,
            $source->openedAt,
            $source->status,
            $source->menu(),
            $source->guests(),
            $source->courses(),
            $source->consumptions(),
            $source->payments(),
        );

        self::assertSame([], $restored->pullEvents());
        self::assertSame($source->id, $restored->id);
        self::assertSame('location-1', $restored->locationId);
        self::assertCount(2, $restored->courses());
    }

    private function service(int $pax): TableService
    {
        return TableService::open(
            'service-1',
            'tenant-1',
            'company-1',
            'location-1',
            'table-4',
            $pax,
            new \DateTimeImmutable('2026-09-14T20:00:00+02:00'),
        );
    }

    private function menu(): MenuTemplate
    {
        return new MenuTemplate(
            'menu-1',
            'Experience',
            15000,
            [
                new CourseTemplate(
                    'course-template-1',
                    1,
                    'Fish',
                    [
                        new PreparationTemplate('prep-fish', 'Fish', 'station-fish', PreparationQuantityMode::PerGuest),
                        new PreparationTemplate('prep-garnish', 'Garnish', 'station-pass', PreparationQuantityMode::Fixed, 1),
                    ],
                ),
                new CourseTemplate(
                    'course-template-2',
                    2,
                    'Dessert',
                    [new PreparationTemplate('prep-dessert', 'Dessert', 'station-pastry', PreparationQuantityMode::PerGuest)],
                ),
            ],
        );
    }

    private function singleCourseMenuWithoutKitchenWork(): MenuTemplate
    {
        return new MenuTemplate(
            'menu-simple',
            'Simple menu',
            15000,
            [new CourseTemplate('course-simple', 1, 'Courtesy', [])],
        );
    }
}
