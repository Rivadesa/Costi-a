<?php

declare(strict_types=1);

namespace App\Infrastructure\Persistence;

use Hospitality\Application\Contracts\OperationalCatalogRepository;
use Illuminate\Support\Facades\DB;

final class LaravelOperationalCatalogRepository implements OperationalCatalogRepository
{
    public function tables(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('dining_tables as t')
            ->join('dining_areas as a', 'a.id', '=', 't.dining_area_id')
            ->where('t.tenant_id', $tenantId)
            ->where('t.company_id', $companyId)
            ->where('t.location_id', $locationId)
            ->where('t.active', true)
            ->orderBy('a.sequence')
            ->orderBy('t.sequence')
            ->get(['t.id', 't.code', 't.name', 't.capacity', 'a.name as area'])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'code' => (string) $row->code,
                'name' => (string) $row->name,
                'capacity' => (int) $row->capacity,
                'area' => (string) $row->area,
            ])->all();
    }

    public function menus(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('menu_templates as m')
            ->leftJoin('course_templates as c', 'c.menu_template_id', '=', 'm.id')
            ->where('m.tenant_id', $tenantId)
            ->where('m.company_id', $companyId)
            ->where(static function ($query) use ($locationId): void {
                $query->whereNull('m.location_id')->orWhere('m.location_id', $locationId);
            })
            ->where('m.active', true)
            ->groupBy('m.id', 'm.name', 'm.price_cents')
            ->orderBy('m.name')
            ->get([
                'm.id',
                'm.name',
                'm.price_cents',
                DB::raw('count(c.id) as course_count'),
            ])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'name' => (string) $row->name,
                'price_cents' => (int) $row->price_cents,
                'course_count' => (int) $row->course_count,
            ])->all();
    }

    public function stations(string $tenantId, string $companyId, string $locationId): array
    {
        return DB::table('kitchen_stations')
            ->where('tenant_id', $tenantId)
            ->where('company_id', $companyId)
            ->where('location_id', $locationId)
            ->where('active', true)
            ->orderBy('sequence')
            ->get(['id', 'name'])
            ->map(static fn ($row): array => [
                'id' => (string) $row->id,
                'name' => (string) $row->name,
            ])->all();
    }
}
