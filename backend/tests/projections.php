<?php

declare(strict_types=1);

require __DIR__ . '/bootstrap.php';

use Hospitality\Application\Kitchen\KdsProjector;
use Hospitality\Application\ServiceBoard\ServiceBoardProjector;
use Hospitality\Domain\Shared\Ulid;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\PreparationTemplate;
use Hospitality\Domain\Service\Restriction;
use Hospitality\Domain\Service\RestrictionSeverity;
use Hospitality\Domain\Service\RestrictionType;
use Hospitality\Domain\Service\TableService;

$station = 'fish';
$menu = new MenuTemplate(Ulid::generate(), 'Experiencia', 10000, [
    new CourseTemplate(Ulid::generate(), 1, 'Pescado', [
        new PreparationTemplate(Ulid::generate(), 'Rodaballo', $station),
    ]),
]);

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
$guest2 = $service->addGuest('Carlos');
$service->addRestriction($guest2->id, new Restriction(Ulid::generate(), 'Marisco', RestrictionType::Allergy, RestrictionSeverity::Critical));
$service->start();
$service->fireNextCourse();

$kds = (new KdsProjector())->stationQueue([$service], $station);
ok(count($kds) === 1, 'KDS projection contains the active table for the station');
ok(count($kds[0]['items']) === 2, 'KDS projection keeps per-guest preparations');
$itemForPax2 = array_values(array_filter($kds[0]['items'], fn ($item) => $item['guest_position'] === 2))[0];
ok($itemForPax2['restrictions'][0]['severity'] === 'critical', 'KDS item carries the critical restriction for the affected guest');

$board = (new ServiceBoardProjector())->project([$service]);
ok($board[0]['course']['status'] === 'fired', 'service board exposes the current course state');
ok(count($board[0]['critical_restrictions']) === 1, 'service board surfaces critical restrictions');

echo "\nAll V1A projection tests passed.\n";
