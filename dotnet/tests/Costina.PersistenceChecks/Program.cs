using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;
using Npgsql;

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
// D3.6: reapertura auditada y devoluciones persisten; un payload anterior sin refunds restaura limpio.
a3.Reopen("bebida tardia",stamp); a3.AddCharge("l3","Café",1,200,stamp);
var a4=SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(Wire.Encode(a3.Snapshot())));
Check(a4.State==AccountState.Open && a4.BalanceCents==200 && a4.View().Charges.Count==3,"reopened account persists with its new charge");
var r=new SettlementAccount("r",scope,"s"); r.AddCharge("l1","Menú",1,15000,stamp); r.RecordPayment("p","card",20000,stamp);
r.Refund("rf","cash",5000,"cobro de más",stamp);
var r2=SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(Wire.Encode(r.Snapshot())));
Check(r2.RefundedCents==5000 && r2.PaidCents==15000 && r2.CreditCents==0 && r2.View().Refunds!.Count==1,"refund persists and nets the payment");
Rejected(()=>SettlementAccount.Restore(r.Snapshot() with {Refunds=[r.View().Refunds![0] with {AmountCents=25000}]}),"refund above recorded payments rejected");
Rejected(()=>SettlementAccount.Restore(r.Snapshot() with {Refunds=[r.View().Refunds![0] with {Reason=" "}]}),"refund without reason rejected");
var legacyAccount=Wire.Decode<AccountSnapshot>(Wire.Encode(a.Snapshot()).Replace(",\"refunds\":[]",""));
Check(SettlementAccount.Restore(legacyAccount).RefundedCents==0,"pre-D3.6 account payload restores without refunds");
var o=new TableOccupancy("o",scope,"M1","s"); o.Release(restored,"salida",stamp);
var o2=TableOccupancy.Restore(Wire.Decode<OccupancySnapshot>(Wire.Encode(o.Snapshot())));
Check(o2.State==OccupancyState.Released && o2.PendingEvents.Count==0,"occupancy restores without phantom events");
Rejected(()=>TableOccupancy.Restore(o.Snapshot() with {ReleasedAt=null}),"released snapshot without timestamp rejected");
var json=Wire.Encode(restored.View());
Check(!json.Contains("paid",StringComparison.OrdinalIgnoreCase) && !json.Contains("price",StringComparison.OrdinalIgnoreCase) && !json.Contains("account",StringComparison.OrdinalIgnoreCase),"operational DTO has no money");
// D3.2: restricciones por comensal persisten en el snapshot; el acuse pendiente sobrevive reinicios.
var dres=new DiningService("sr",scope,"M2",2,[new("p1","Pase",[new("a","Plato","hot",GuestPosition:1)])]);
dres.DeclareRestriction(1,RestrictionKind.Allergy,"marisco",RestrictionSeverity.Severe,stamp);
dres.ReviewPreparation("p1","a",ReviewDecision.Unaffected,"no lleva marisco",stamp);   // E4e: el pase no sale sin la decision de cocina
dres.Start(stamp); dres.FireNext(stamp);
dres.DeclareRestriction(null,RestrictionKind.Preference,"sin cilantro",RestrictionSeverity.Mild,stamp);
var dres2=DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(dres.Snapshot())));
Check(dres2.RestrictionsPendingAck && dres2.Restrictions.Count==2,"restriction change survives restart still unacknowledged");
Check(Wire.Encode(dres2.Snapshot())==Wire.Encode(dres.Snapshot()),"restriction round trip is lossless");
Check(dres2.View().Courses[0].Preparations[0].ReviewPending,"pending review per preparation survives restart");
dres2.ReviewPreparation("p1","a",ReviewDecision.Adapt,"sin cilantro",stamp);
var dres3=DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(dres2.Snapshot())));
Check(!dres3.RestrictionsPendingAck && dres3.View().Courses[0].Preparations[0].Review?.Decision==ReviewDecision.Adapt,"review decision persists and clears the pending flag");
// D3.5: un payload D3.2 (acuse global pendiente, sin marcas por elaboracion) restaura con TODAS las elaboraciones enviadas pendientes.
var legacyAck=Wire.Decode<DiningSnapshot>(Wire.Encode(dres.Snapshot()).Replace("\"reviewPending\":true","\"reviewPending\":false"));
Check(legacyAck.RestrictionsPendingAck && DiningService.Restore(legacyAck).View().Courses[0].Preparations.All(p=>p.ReviewPending),"pre-D3.5 payload with global acknowledgement restores every sent preparation as pending review");
Check(!Wire.Encode(dres.Snapshot()).Contains("\"restrictions\":null"),"snapshot stores the restriction list itself");
var legacy=Wire.Decode<DiningSnapshot>(Wire.Encode(d.Snapshot()) .Replace(",\"restrictions\":[],\"restrictionsPendingAck\":false",""));
Check(DiningService.Restore(legacy).Restrictions.Count==0,"pre-D3.2 payload without restriction fields restores cleanly");
Rejected(()=>DiningService.Restore(dres.Snapshot() with {Restrictions=[new("id",5,RestrictionKind.Allergy,"x",RestrictionSeverity.Severe)]}),"restriction outside pax rejected");
var openSnap=new DiningService("so",scope,"M3",1,[new("c","c",[new("p","p","cold")])]).Snapshot();
Rejected(()=>DiningService.Restore(openSnap with {RestrictionsPendingAck=true}),"pending acknowledgement on inactive service rejected");
// E4e (ADR-016): una mesa SENTADA con revisiones en pases sin enviar se guarda y se vuelve a leer. Un motor anterior no la lee: sin vuelta atras.
var seated=new DiningService("se",scope,"M5",2,[new("p1","Aperitivo",[new("ostra-1","Ostra","cold",GuestPosition:1),new("ostra-2","Ostra","cold",GuestPosition:2)]),
    new("p2","Principal",[new("merluza-1","Merluza","hot",GuestPosition:1)])]);
seated.DeclareRestriction(1,RestrictionKind.Allergy,"marisco",RestrictionSeverity.Severe,stamp);
seated.ReviewPreparation("p1","ostra-1",ReviewDecision.Adapt,"vieira",stamp);
var seated2=DiningService.Restore(Wire.Decode<DiningSnapshot>(Wire.Encode(seated.Snapshot())));
Check(seated2.State==DiningState.Open && seated2.RestrictionsPendingAck && seated2.View().Courses[1].Preparations[0].ReviewPending,"seated table with a review pending on an unsent course survives restart");
Check(Wire.Encode(seated2.Snapshot())==Wire.Encode(seated.Snapshot()),"seated review round trip is lossless");
Check(seated2.View().Courses[0].Preparations.Single(p=>p.Id=="ostra-1").Review?.Note=="vieira" && !seated2.View().Courses[0].Preparations.Single(p=>p.Id=="ostra-2").ReviewPending,"decision taken on an unsent course persists");
Rejected(()=>DiningService.Restore(seated.Snapshot() with {State=DiningState.Cancelled}),"pending review on a cancelled table rejected");
// Una mesa guardada por 0.43.0-c3c (antes de E4e), tal cual la escribia aquel motor: se lee igual y su pase sin enviar NO queda marcado
// (una mesa abierta al actualizar conserva su estado; se marcara con el siguiente cambio de restricciones).
const string c3c = """{"id":"s35","scope":{"tenantId":"tenant","companyId":"company","locationId":"location"},"tableId":"M4","pax":2,"state":"InService","startedAt":"2026-10-20T20:15:00+00:00","completedAt":null,"courses":[{"id":"p1","name":"Aperitivo","state":"Fired","firedAt":"2026-10-20T20:15:00+00:00","readyAt":null,"servedAt":null,"skipReason":null,"preparations":[{"id":"ostra-1","name":"Ostra","stationId":"cold","quantity":1,"guestPosition":1,"mandatory":true,"state":"Fired","reviewPending":false,"review":{"decision":"Adapt","note":"vieira en vez de ostra","at":"2026-10-20T20:15:00+00:00"}},{"id":"pan","name":"Pan","stationId":"hot","quantity":1,"guestPosition":null,"mandatory":true,"state":"Fired","reviewPending":true}],"choiceRequired":false,"optional":false},{"id":"p2","name":"Principal","state":"Pending","firedAt":null,"readyAt":null,"servedAt":null,"skipReason":null,"preparations":[{"id":"merluza-2","name":"Merluza","stationId":"hot","quantity":1,"guestPosition":2,"mandatory":true,"state":"Pending","reviewPending":false}],"choiceRequired":false,"optional":false}],"restrictions":[{"id":"c514b92645784f6fa5e0f7b9e03fc0a9","guestPosition":1,"kind":"Allergy","substance":"marisco","severity":"Severe"},{"id":"5d61480c7c3e4a8c9c6677dea4e3295f","guestPosition":null,"kind":"Preference","substance":"sin cilantro","severity":"Mild"}],"restrictionsPendingAck":true,"offerId":"LAB-TASTING"}""";
var c3cTable=DiningService.Restore(Wire.Decode<DiningSnapshot>(c3c)).View();
Check(c3cTable.RestrictionsPendingAck && c3cTable.Restrictions!.Count==2 && c3cTable.Courses[0].Preparations.Single(p=>p.Id=="pan").ReviewPending
    && c3cTable.Courses[0].Preparations.Single(p=>p.Id=="ostra-1").Review?.Decision==ReviewDecision.Adapt && !c3cTable.Courses[1].Preparations[0].ReviewPending,
    "a 0.43.0-c3c payload restores unchanged and its unsent course stays unmarked");
// D3.1: las affordances se calculan al leer y NUNCA entran en los payloads persistidos.
foreach(var payload in new[]{Wire.Encode(d.Snapshot()),Wire.Encode(restored.Snapshot()),Wire.Encode(a2.Snapshot()),Wire.Encode(o.Snapshot())})
    Check(!payload.Contains("actions",StringComparison.OrdinalIgnoreCase),"snapshot payload has no affordances");
Check(Wire.Encode(restored.View())==Wire.Encode(DiningService.Restore(restored.Snapshot()).View()),"plain view stays action-free after restore");
// D4.1: hash de contrasena PBKDF2 puro (sin base de datos).
var stored=PasswordHashing.Hash("una-contrasena-de-ensayo");
Check(stored.StartsWith("pbkdf2-sha256.210000.") && !stored.Contains("una-contrasena"),"password hash stores no plaintext");
Check(PasswordHashing.Verify("una-contrasena-de-ensayo",stored),"correct password verifies");
Check(!PasswordHashing.Verify("otra-contrasena-distinta",stored),"wrong password fails");
Check(!PasswordHashing.Verify("una-contrasena-de-ensayo","basura.sin.formato"),"malformed stored hash fails closed");
Check(PasswordHashing.Hash("x")!=PasswordHashing.Hash("x"),"salt makes every hash unique");
// C3c: codigo CORTO de emparejamiento para teclear en el icono de iPhone y iPad (puro, sin base de datos).
Check(DeviceStore.ShortAlphabet=="23456789ABCDEFGHJKLMNPQRSTUVWXYZ" && DeviceStore.ShortAlphabet.Distinct().Count()==32,"short code alphabet has 32 unambiguous symbols");
var shortCodes=Enumerable.Range(0,500).Select(_=>DeviceStore.ShortCode()).ToList();
Check(shortCodes.All(c=>c.Length==10 && c.All(DeviceStore.ShortAlphabet.Contains)),"short code is 10 symbols of the alphabet");
Check(shortCodes.Distinct().Count()==500 && DeviceStore.ShortAlphabet.All(s=>shortCodes.Any(c=>c.Contains(s))),"short codes are random over the whole alphabet");
foreach(var (typed,canonical) in new[]{("K7M2Q9XR4T","K7M2Q9XR4T"),("k7m2q-9xr4t","K7M2Q9XR4T"),(" K7M2Q 9XR4T ","K7M2Q9XR4T"),("k7m2q - 9xr4t","K7M2Q9XR4T")})
    Check(DeviceStore.ShortForm(typed)==canonical,"typed short code '"+typed+"' is read in any case, with spaces or hyphens");
// Nada que no sean 10 simbolos del alfabeto: ni el codigo largo (base64url, distingue mayusculas), ni 0/O/1/I, ni otros separadores.
foreach(var sample in new[]{"","K7M2Q9XR4","K7M2Q9XR4TT","K7M2Q9XR4O","K7M2Q9XR41","K7M2Q9XR4I","K7M2Q_9XR4T","K7M2Q.9XR4T",(char)0x017F+"7M2Q9XR4T","K7M2Q9XR4"+(char)0x0422,"kjYLiRkxgaTdekLGDPTDuj-sz3kr1xM5j8QVQmkW-DU","2345678923-AB"})
    Check(DeviceStore.ShortForm(sample) is null,"not a short code: '"+sample+"'");
// La huella del codigo es UNICA en toda la tabla (tambien filas viejas): si una emision choca, se emite otro codigo; otro error no se reintenta.
PostgresException Unique() => new("duplicate key value violates unique constraint","ERROR","ERROR",PostgresErrorCodes.UniqueViolation);
var offered=new Queue<string>(["CHOCA","NUEVO","OTRO"]); var inserted=new List<string>();
var fresh=await DeviceStore.IssueFreshAsync(()=>offered.Dequeue(),code=>{ inserted.Add(code);
    return code=="CHOCA" ? throw Unique() : Task.FromResult(new PairingIssued("id",code,DateTimeOffset.UtcNow)); });
Check(fresh.Code=="NUEVO" && inserted.SequenceEqual(["CHOCA","NUEVO"]),"a code whose hash collides is replaced by a fresh one");
var attempts=0;
try { await DeviceStore.IssueFreshAsync(()=>"SIEMPRE",_=>{ attempts++; throw Unique(); }); Check(false,"endless collisions must fail"); }
catch(PostgresException) { Check(attempts==DeviceStore.IssueAttempts,"collisions are retried a bounded number of times"); }
attempts=0;
try { await DeviceStore.IssueFreshAsync(()=>"OTRO",_=>{ attempts++; throw new PostgresException("other","ERROR","ERROR",PostgresErrorCodes.ForeignKeyViolation); }); Check(false,"other errors must fail"); }
catch(PostgresException) { Check(attempts==1,"any other storage error is not retried"); }
Console.WriteLine($"D1 snapshots: {checks}/{checks} passed.");
