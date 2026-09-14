<?php

declare(strict_types=1);

namespace Hospitality\Application\Auth;

final class PermissionCatalog
{
    /** @return list<string> */
    public static function administrator(): array
    {
        return ['*'];
    }

    /** @return list<string> */
    public static function maitre(): array
    {
        return [
            'service.view', 'service.open', 'service.edit', 'service.manage', 'service.close',
            'course.fire', 'course.serve', 'course.modify',
            'consumption.add', 'consumption.cancel', 'payment.record',
            'kitchen.view', 'kitchen.pass',
        ];
    }

    /** @return list<string> */
    public static function waiter(): array
    {
        return [
            'service.view', 'service.open', 'service.edit',
            'course.fire', 'course.serve',
            'consumption.add',
            'kitchen.view',
        ];
    }

    /** @return list<string> */
    public static function chef(): array
    {
        return [
            'service.view', 'service.manage',
            'course.fire', 'course.modify',
            'kitchen.view', 'kitchen.update', 'kitchen.pass',
        ];
    }

    /** @return list<string> */
    public static function kitchen(): array
    {
        return ['kitchen.view', 'kitchen.update'];
    }

    /** @return array<string, list<string>> */
    public static function presets(): array
    {
        return [
            'Administrator' => self::administrator(),
            'Maître' => self::maitre(),
            'Waiter' => self::waiter(),
            'Chef' => self::chef(),
            'Kitchen' => self::kitchen(),
        ];
    }
}
