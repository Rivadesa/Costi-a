<?php

declare(strict_types=1);
namespace Hospitality\Tests\Feature;
use Database\Seeders\RetiroDemoSeeder;
use Hospitality\Tests\TestCase;
use Hospitality\Domain\Shared\Ulid;
use Illuminate\Foundation\Testing\DatabaseTransactions;
use Illuminate\Support\Facades\DB;

final class LifecycleApiTest extends TestCase
{
    use DatabaseTransactions;
    private function login(array $permissions = ['*']): string
    {
        $this->artisan('db:seed',['--force'=>true])->assertExitCode(0);
        config(['hospitality.tenant_id'=>RetiroDemoSeeder::TENANT_ID,'hospitality.company_id'=>RetiroDemoSeeder::COMPANY_ID,'hospitality.location_id'=>RetiroDemoSeeder::LOCATION_ID]);
        DB::table('roles')->where('id',RetiroDemoSeeder::ROLE_ID)->update(['permissions'=>json_encode($permissions,JSON_THROW_ON_ERROR)]);
        return $this->postJson('/api/v1/auth/login',['email'=>'demo@hospitality.local','password'=>'demo1234'])->assertOk()->json('access_token');
    }
    private function command(string $token, string $path, array $body = [], ?string $key = null)
    {
        return $this->withToken($token)->withHeader('Idempotency-Key',$key??Ulid::generate())->postJson('/api/v1/'.$path,$body);
    }
    private function open(string $token): string
    {
        // No menu: this test isolates lifecycle and account actions from KDS fixtures.
        return $this->command($token,'services',['table_id'=>'01DEMO00000000000000000017','pax'=>1])->assertCreated()->json('service_id');
    }
    public function test_released_unpaid_account_remains_accessible_and_each_transition_is_audited_once(): void
    {
        $token=$this->login(); $id=$this->open($token);
        $this->command($token,"checkout/services/$id/consumptions",['product_id'=>'01DEMO00000000000000000071','quantity'=>1])->assertOk();
        $this->command($token,"services/$id/complete")->assertOk()->assertJsonPath('status','closed')->assertJsonMissingPath('subtotal_cents');
        $key=Ulid::generate();
        $this->command($token,"services/$id/release-table",['reason'=>'Guests left'],$key)->assertOk();
        $this->command($token,"services/$id/release-table",['reason'=>'Guests left'],$key)->assertOk();
        $this->withToken($token)->getJson('/api/v1/service-board')->assertOk();
        $accounts=$this->withToken($token)->getJson('/api/v1/checkout/services')->assertOk()->json('data');
        self::assertContains($id,array_column($accounts,'service_id'));
        $account=$this->withToken($token)->getJson("/api/v1/checkout/services/$id")->assertOk()->json();
        self::assertSame(400,$account['balance_cents']);
        self::assertSame('released',$account['occupancy_status']);
        $this->command($token,"checkout/services/$id/close-account")->assertConflict();
        $this->command($token,"checkout/services/$id/payments",['method'=>'card','amount_cents'=>400])->assertOk();
        $closeKey=Ulid::generate();
        $this->command($token,"checkout/services/$id/close-account",[],$closeKey)->assertOk();
        $this->command($token,"checkout/services/$id/close-account",[],$closeKey)->assertOk();
        foreach (['service.completed','table.released','account.closed'] as $type) {
            self::assertSame(1,DB::table('outbox_events')->where('aggregate_id',$id)->where('event_type',$type)->count());
            self::assertSame(1,DB::table('audit_log')->where('entity_id',$id)->where('action',$type)->count());
            self::assertSame(RetiroDemoSeeder::USER_ID,DB::table('audit_log')->where('entity_id',$id)->where('action',$type)->value('user_id'));
        }
        $this->command($token,"checkout/services/$id/reopen-account",['reason'=>'Missing item'])->assertOk();
        self::assertSame(1,DB::table('payments')->where('table_service_id',$id)->count());
        $newId=$this->open($token); self::assertNotSame($newId,$id);
        self::assertSame(1,DB::table('table_services')->where('dining_table_id','01DEMO00000000000000000017')->where('occupancy_status','occupied')->count());
    }
    public function test_occupied_table_rejects_second_open_without_duplicate_children(): void
    {
        $token=$this->login(); $id=$this->open($token);
        $this->command($token,'services',['table_id'=>'01DEMO00000000000000000017','pax'=>2])->assertConflict();
        self::assertSame(1,DB::table('table_services')->where('dining_table_id','01DEMO00000000000000000017')->count());
        self::assertSame(1,DB::table('service_guests')->where('table_service_id',$id)->count());
    }
    public function test_new_actions_require_separate_server_permissions_and_old_close_cannot_mutate(): void
    {
        $token=$this->login(['service.open','service.view','service.close']); $id=$this->open($token);
        foreach (["services/$id/complete","services/$id/release-table","services/$id/review-lifecycle","checkout/services/$id/close-account","checkout/services/$id/reopen-account"] as $path) {
            $this->command($token,$path,['reason'=>'Test','status'=>'open'])->assertForbidden();
        }
        $this->withToken($token)->getJson('/api/v1/checkout/services')->assertForbidden();
        $this->command($token,"checkout/services/$id/close")->assertConflict()->assertJsonPath('error','lifecycle_upgrade_required');
        self::assertSame('open',DB::table('table_services')->where('id',$id)->value('status'));
        self::assertSame('occupied',DB::table('table_services')->where('id',$id)->value('occupancy_status'));
    }
    public function test_early_payment_is_persisted_without_changing_pacing_or_table_state(): void
    {
        $token=$this->login(); $id=$this->open($token);
        $this->command($token,"checkout/services/$id/consumptions",['product_id'=>'01DEMO00000000000000000071','quantity'=>2])->assertOk();
        $this->command($token,"checkout/services/$id/payments",['method'=>'cash','amount_cents'=>400])->assertOk();
        $this->withToken($token)->getJson("/api/v1/services/$id")->assertOk()->assertJsonPath('status','open')->assertJsonMissingPath('settlement_status');
        $this->withToken($token)->getJson("/api/v1/checkout/services/$id")->assertOk()->assertJsonPath('settlement_status','partially_paid');
        $this->command($token,"checkout/services/$id/payments",['method'=>'card','amount_cents'=>400])->assertOk();
        self::assertSame('open',DB::table('table_services')->where('id',$id)->value('status'));
        self::assertNull(DB::table('table_services')->where('id',$id)->value('started_at'));
    }
}
