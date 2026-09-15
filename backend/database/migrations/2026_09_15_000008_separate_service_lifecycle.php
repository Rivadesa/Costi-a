<?php

declare(strict_types=1);
use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    /** Stop all old writers and take a verified backup BEFORE this migration. */
    public function up(): void
    {
        // Entire change, including preflight, is atomic even when invoked directly.
        DB::transaction(function (): void {
            DB::statement('LOCK TABLE table_services IN ACCESS EXCLUSIVE MODE');
            $duplicate = DB::table('table_services')->select('dining_table_id')->whereNotIn('status', ['closed', 'cancelled'])
                ->groupBy('tenant_id', 'company_id', 'location_id', 'dining_table_id')
                ->havingRaw('COUNT(*) > 1')->exists();
            if ($duplicate) throw new \RuntimeException('D0 preflight: duplicate occupied tables. Reconcile explicitly; no records were merged or removed.');
            $inconsistent = DB::table('table_services as s')->whereIn('s.status', ['closed', 'cancelled'])
                ->whereExists(function ($q): void {
                    $q->selectRaw('1')->from('service_courses as c')->whereColumn('c.table_service_id', 's.id')
                        ->whereIn('c.status', ['fired', 'preparing', 'ready']);
                })->exists();
            if ($inconsistent) throw new \RuntimeException('D0 preflight: ended legacy service still has active kitchen work. Reconcile before migration.');
            if (DB::table('table_services')->where('status', 'closed')->whereNull('closed_at')->exists()) {
                throw new \RuntimeException('D0 preflight: historic closed service lacks closed_at; do not invent a timestamp.');
            }
            Schema::create('service_lifecycle_legacy', function (Blueprint $table): void {
                $table->char('service_id', 26)->primary();
                $table->jsonb('snapshot');
                $table->timestampTz('captured_at')->useCurrent();
                $table->foreign('service_id')->references('id')->on('table_services');
            });
            DB::statement('INSERT INTO service_lifecycle_legacy (service_id, snapshot) SELECT id, to_jsonb(s) FROM table_services s');
            Schema::table('table_services', function (Blueprint $table): void {
                $table->string('occupancy_status', 16)->default('occupied');
                $table->timestampTz('released_at')->nullable();
                $table->timestampTz('account_closed_at')->nullable();
                $table->boolean('lifecycle_review_required')->default(false);
            });
            // Paid/pending_payment erased whether service was open, paused or active.
            // Quarantine those records; only a reasoned authorized review can resume.
            DB::statement("UPDATE table_services SET status = 'paused', lifecycle_review_required = TRUE WHERE status IN ('paid','pending_payment')");
            DB::statement("UPDATE table_services SET occupancy_status = 'released', released_at = COALESCE(closed_at, cancelled_at), account_closed_at = CASE WHEN status = 'closed' THEN closed_at ELSE NULL END WHERE status IN ('closed','cancelled')");
            DB::statement('ALTER TABLE table_services DROP CONSTRAINT table_services_status_check');
            DB::statement("ALTER TABLE table_services ADD CONSTRAINT table_services_status_check CHECK (status IN ('prepared','open','in_service','paused','closed','cancelled'))");
            DB::statement("ALTER TABLE table_services ADD CONSTRAINT table_services_occupancy_check CHECK (occupancy_status IN ('occupied','released'))");
            DB::statement("ALTER TABLE table_services ADD CONSTRAINT table_services_release_check CHECK (occupancy_status <> 'released' OR status IN ('closed','cancelled'))");
            DB::statement("CREATE UNIQUE INDEX table_services_one_occupant ON table_services (tenant_id, company_id, location_id, dining_table_id) WHERE occupancy_status = 'occupied'");
            DB::statement('CREATE INDEX table_services_open_accounts ON table_services (tenant_id, company_id, location_id, opened_at) WHERE account_closed_at IS NULL');
        });
    }

    public function down(): void
    {
        // A lossy rollback could merge independent axes and corrupt operational truth.
        throw new \RuntimeException('D0 has no automatic lossy downgrade. Restore the verified pre-upgrade backup with all writers stopped, or apply a reviewed forward fix.');
    }
};
