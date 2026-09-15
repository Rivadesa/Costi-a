<?php

declare(strict_types=1);
use Illuminate\Contracts\Console\Kernel;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;
use Database\Seeders\RetiroDemoSeeder;
use Hospitality\Domain\Shared\Ulid;
require __DIR__.'/../vendor/autoload.php';
$app=require __DIR__.'/../bootstrap/app.php'; $app->make(Kernel::class)->bootstrap();
if (!$app->environment('testing')) throw new RuntimeException('Disposable migration test requires APP_ENV=testing.');
function migrationCheck(bool $ok,string $message): void {
    if (!$ok) throw new RuntimeException('FAIL: '.$message);
    echo "✓ $message\n";
}
// Dedicated disposable schema: never drop or migrate the caller's business schema.
$schema='d0_migration_'.strtolower(Ulid::generate());
$oldPath=DB::selectOne('SHOW search_path')->search_path;
DB::statement('CREATE SCHEMA "'.$schema.'"');
DB::statement('SET search_path TO "'.$schema.'"');
try {
    foreach (glob(__DIR__.'/../database/migrations/2026_09_14_*.php') as $file) (require $file)->up();
    $app->make(\Database\Seeders\DatabaseSeeder::class)->setContainer($app)->run();
    $states=['prepared','open','in_service','paused','pending_payment','paid','closed','cancelled']; $ids=[];
    foreach ($states as $i=>$state) {
        $id=Ulid::generate(); $ids[$state]=$id;
        DB::table('table_services')->insert(['id'=>$id,'tenant_id'=>RetiroDemoSeeder::TENANT_ID,'company_id'=>RetiroDemoSeeder::COMPANY_ID,
            'location_id'=>RetiroDemoSeeder::LOCATION_ID,'dining_table_id'=>'01DEMO000000000000000000'.(10+$i),'status'=>$state,'pax'=>1,
            'opened_at'=>'2026-09-14T18:00:00Z','closed_at'=>$state==='closed'?'2026-09-14T20:00:00Z':null,
            'cancelled_at'=>$state==='cancelled'?'2026-09-14T19:00:00Z':null,'version'=>7]);
    }
    $paymentId=Ulid::generate();
    DB::table('payments')->insert(['id'=>$paymentId,'table_service_id'=>$ids['paid'],'method'=>'card','amount_cents'=>15000]);
    $idemId=Ulid::generate();
    DB::table('idempotency_keys')->insert(['id'=>$idemId,'tenant_id'=>RetiroDemoSeeder::TENANT_ID,'idempotency_key'=>'legacy-payment',
        'command_name'=>'payment.record','request_hash'=>str_repeat('a',64),'response_status'=>200,'response_body'=>'{"service_status":"paid","paid_cents":15000}']);
    $beforePayment=(array)DB::table('payments')->where('id',$paymentId)->first();
    $beforeIdem=(array)DB::table('idempotency_keys')->where('id',$idemId)->first();
    $duplicateId=Ulid::generate();
    $duplicate=(array) DB::table('table_services')->where('id',$ids['open'])->first(); $duplicate['id']=$duplicateId;
    DB::table('table_services')->insert($duplicate);
    $migration=require __DIR__.'/../database/migrations/2026_09_15_000008_separate_service_lifecycle.php';
    $rejected=false;
    try { $migration->up(); } catch (RuntimeException $e) { $rejected=str_contains($e->getMessage(),'duplicate occupied'); }
    migrationCheck($rejected,'duplicate legacy occupants abort rather than auto-delete/merge');
    migrationCheck(!Schema::hasColumn('table_services','occupancy_status'),'failed migration leaves legacy schema intact');
    // Remove only the synthetic duplicate created by THIS isolated test.
    DB::table('table_services')->where('id',$duplicateId)->delete();
    $migration->up();
    migrationCheck(DB::table('table_services')->count()===8,'migration preserves all eight legacy services');
    migrationCheck(DB::table('service_lifecycle_legacy')->count()===8,'original lifecycle snapshots retained');
    foreach ($ids as $state=>$id) {
        $row=DB::table('table_services')->where('id',$id)->first();
        $snapshot=json_decode(DB::table('service_lifecycle_legacy')->where('service_id',$id)->value('snapshot'),true,flags:JSON_THROW_ON_ERROR);
        migrationCheck($snapshot['status']===$state && (int)$row->version===7,'original '.$state.' status and version preserved');
        if (in_array($state,['paid','pending_payment'],true)) {
            migrationCheck($row->status==='paused' && $row->lifecycle_review_required && $row->occupancy_status==='occupied','ambiguous '.$state.' requires review without guessing');
        }
    }
    migrationCheck((array)DB::table('payments')->where('id',$paymentId)->first()===$beforePayment,'historic payment remains byte-for-byte equal in its row');
    migrationCheck((array)DB::table('idempotency_keys')->where('id',$idemId)->first()===$beforeIdem,'historic idempotent response remains unchanged');
    $closed=DB::table('table_services')->where('id',$ids['closed'])->first();
    migrationCheck($closed->occupancy_status==='released' && $closed->account_closed_at===$closed->closed_at,'old completed settlement maps explicitly to released/closed account');
    $repo=new \App\Infrastructure\Persistence\LaravelTableServiceRepository();
    $loaded=$repo->get(RetiroDemoSeeder::TENANT_ID,RetiroDemoSeeder::COMPANY_ID,RetiroDemoSeeder::LOCATION_ID,$ids['paid']);
    migrationCheck($loaded->lifecycleReviewRequired && $loaded->paidCents()===15000 && $loaded->pullEvents()===[],'migrated aggregate roundtrip retains payments without fake events');
    DB::transaction(fn()=> $repo->save($loaded));
    migrationCheck($repo->get(RetiroDemoSeeder::TENANT_ID,RetiroDemoSeeder::COMPANY_ID,RetiroDemoSeeder::LOCATION_ID,$ids['paid'])->lifecycleReviewRequired,'review flag survives a save/load cycle');
} finally {
    DB::statement("SELECT set_config('search_path', ?, false)",[$oldPath]);
    DB::statement('DROP SCHEMA "'.$schema.'" CASCADE');
}
echo "\nD0 PostgreSQL migration checks passed.\n";
