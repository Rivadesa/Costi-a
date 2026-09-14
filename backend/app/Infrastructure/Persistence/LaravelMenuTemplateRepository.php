<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\MenuTemplateRepository;
use Hospitality\Domain\Service\CourseTemplate;
use Hospitality\Domain\Service\MenuTemplate;
use Hospitality\Domain\Service\PreparationQuantityMode;
use Hospitality\Domain\Service\PreparationTemplate;
use Illuminate\Support\Facades\DB;

final class LaravelMenuTemplateRepository implements MenuTemplateRepository
{
    public function get(
        string $tenantId,
        string $companyId,
        string $locationId,
        string $menuId,
    ): MenuTemplate {
        $menu = DB::table('menu_templates')
            ->where('id', $menuId)
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('active', true)
            ->where(function ($query) use ($locationId): void {
                $query->whereNull('location_id')->orWhere('location_id', $locationId);
            })
            ->first();

        if ($menu === null) {
            throw new \DomainException('Menu template not found in the requested tenant/company/location.');
        }

        $courseRows = DB::table('course_templates')
            ->where('menu_template_id', $menuId)
            ->where('active', true)
            ->orderBy('sequence')
            ->get();

        if ($courseRows->isEmpty()) {
            throw new \DomainException('Menu template has no active courses.');
        }

        $preparationsByCourse = [];
        foreach (DB::table('preparation_templates')
            ->whereIn('course_template_id', $courseRows->pluck('id')->all())
            ->orderBy('sequence')
            ->get() as $row) {
            $preparationsByCourse[(string) $row->course_template_id][] = new PreparationTemplate(
                (string) $row->id,
                (string) $row->name,
                (string) $row->station_id,
                PreparationQuantityMode::from((string) $row->quantity_mode),
                (int) $row->fixed_quantity,
                (bool) $row->mandatory,
            );
        }

        $courses = [];
        foreach ($courseRows as $row) {
            $courses[] = new CourseTemplate(
                (string) $row->id,
                (int) $row->sequence,
                (string) $row->name,
                $preparationsByCourse[(string) $row->id] ?? [],
            );
        }

        return new MenuTemplate(
            (string) $menu->id,
            (string) $menu->name,
            (int) $menu->price_cents,
            $courses,
        );
    }
}
