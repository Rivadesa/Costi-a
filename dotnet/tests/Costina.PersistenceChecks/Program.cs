using Costina.Domain;
using Costina.Persistence;

var scope=new BusinessScope("tenant","company","location");
var stamp=new CommandStamp(scope,"actor",DateTimeOffset.UtcNow);
var checks=0;
void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; Console.WriteLine("PASS "+message); }
void Rejected(Action action,string message) { try { action(); } catch(RuleViolation) { Check(true,message); return; } throw new Exception(message); }
var d=new DiningService("s",scope,"M1",2,[new("p1","Pase",[new("a","Plato","hot")])]);
Check(DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(d.Snapshot()))).PendingEvents.Count==0,"open restore emits no events");
d.Start(stamp); d.FireNext(stamp); d.StartPreparation("p1","a",stamp); d.Pause("ritmo",stamp);
var restored=DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(d.Snapshot())));
Check(Wire.Encode(restored.Snapshot())==Wire.Encode(d.Snapshot()),"paused service round trip retains all state");
Check(restored.PendingEvents.Count==0,"paused restore emits no events");
restored.ReadyPreparation("p1","a",stamp); restored.ValidateReady("p1",stamp); restored.Serve("p1",stamp); restored.Complete(stamp);
Check(DiningService.Restore(restored.Snapshot()).State==DiningState.Completed,"completed restore");
Rejected(()=>DiningService.Restore(d.Snapshot() with {State=DiningState.Completed,CompletedAt=stamp.At}),"unfinished completed snapshot rejected");
Rejected(()=>DiningService.Restore(d.Snapshot() with {State=(DiningState)123}),"invalid enum rejected");
var snapshot=d.Snapshot();
var corrupt=snapshot.Courses[0] with {State=CourseState.Ready,ReadyAt=stamp.At};
Rejected(()=>DiningService.Restore(snapshot with {Courses=[corrupt]}),"ready with incomplete preparation rejected");
var a=new SettlementAccount("a",scope,"s"); a.AddCharge("l1","Menú",2,15000,stamp); a.RecordPayment("pay","card",30000,stamp);
var a2=SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(Wire.Encode(a.Snapshot())));
Check(a2.PendingEvents.Count==0 && a2.PaidCents==30000,"account rehydration keeps payment without new events");
a2.AddCharge("l2","Agua",1,400,stamp); Check(a2.BalanceCents==400 && restored.State==DiningState.Completed,"charge leaves dining untouched");
a2.VoidCharge("l2","error",stamp); a2.Close(stamp);
var a3=SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(Wire.Encode(a2.Snapshot())));
Check(a3.State==AccountState.Closed && a3.View().Charges.Count==2 && a3.View().Charges[1].Voided,"closed account preserves voided history");
Rejected(()=>SettlementAccount.Restore(a.Snapshot() with {State=AccountState.Closed,Payments=[]}),"closed debt rejected");
Rejected(()=>SettlementAccount.Restore(a.Snapshot() with {Charges=[a.View().Charges[0],a.View().Charges[0]]}),"duplicate persisted charges rejected");
Rejected(()=>SettlementAccount.Restore(a.Snapshot() with {Payments=[a.View().Payments[0] with {AmountCents=-1}]}),"negative persisted payment rejected");
var o=new TableOccupancy("o",scope,"M1","s"); o.Release(restored,"salida",stamp);
var o2=TableOccupancy.Restore(Wire.Decode<OccupancySnapshot>(Wire.Encode(o.Snapshot())));
Check(o2.State==OccupancyState.Released && o2.PendingEvents.Count==0,"occupancy restores without phantom events");
Rejected(()=>TableOccupancy.Restore(o.Snapshot() with {ReleasedAt=null}),"released snapshot without timestamp rejected");
var json=Wire.Encode(restored.View());
Check(!json.Contains("paid",StringComparison.OrdinalIgnoreCase) && !json.Contains("price",StringComparison.OrdinalIgnoreCase) && !json.Contains("account",StringComparison.OrdinalIgnoreCase),"operational DTO has no money");
// D3.2: restricciones por comensal persisten en el snapshot; el acuse pendiente sobrevive reinicios.
var dres=new DiningService("sr",scope,"M2",2,[new("p1","Pase",[new("a","Plato","hot",GuestPosition:1)])]);
dres.DeclareRestriction(1,RestrictionKind.Allergy,"marisco",RestrictionSeverity.Severe,stamp);
dres.Start(stamp); dres.FireNext(stamp);
dres.DeclareRestriction(null,RestrictionKind.Preference,"sin cilantro",RestrictionSeverity.Mild,stamp);
var dres2=DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(dres.Snapshot())));
Check(dres2.RestrictionsPendingAck && dres2.Restrictions.Count==2,"restriction change survives restart still unacknowledged");
Check(Wire.Encode(dres2.Snapshot())==Wire.Encode(dres.Snapshot()),"restriction round trip is lossless");
dres2.AcknowledgeRestrictions(stamp);
Check(!DiningService.Restore(dres2.Snapshot()).RestrictionsPendingAck,"acknowledgement persists");
Check(!Wire.Encode(dres.Snapshot()).Contains("\"restrictions\":null"),"snapshot stores the restriction list itself");
var legacy=Wire.Decode<DiningSnapshot>(Wire.Encode(d.Snapshot()) .Replace(",\"restrictions\":[],\"restrictionsPendingAck\":false",""));
Check(DiningService.Restore(legacy).Restrictions.Count==0,"pre-D3.2 payload without restriction fields restores cleanly");
Rejected(()=>DiningService.Restore(dres.Snapshot() with {Restrictions=[new("id",5,RestrictionKind.Allergy,"x",RestrictionSeverity.Severe)]}),"restriction outside pax rejected");
var openSnap=new DiningService("so",scope,"M3",1,[new("c","c",[new("p","p","cold")])]).Snapshot();
Rejected(()=>DiningService.Restore(openSnap with {RestrictionsPendingAck=true}),"pending acknowledgement on inactive service rejected");
// D3.1: las affordances se calculan al leer y NUNCA entran en los payloads persistidos.
foreach(var payload in new[]{Wire.Encode(d.Snapshot()),Wire.Encode(restored.Snapshot()),Wire.Encode(a2.Snapshot()),Wire.Encode(o.Snapshot())})
    Check(!payload.Contains("actions",StringComparison.OrdinalIgnoreCase),"snapshot payload has no affordances");
Check(Wire.Encode(restored.View())==Wire.Encode(DiningService.Restore(restored.Snapshot()).View()),"plain view stays action-free after restore");
Console.WriteLine($"D1 snapshots: {checks}/{checks} passed.");
