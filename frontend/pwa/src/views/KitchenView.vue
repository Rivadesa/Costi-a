<script lang="ts">
import { reactive } from 'vue'
import { Shift, type AudioLike, type ScreenEnv, type ShiftStatus } from '../turno'

// C3d: UN turno por pagina cargada, no por montaje. Si la pantalla se desmonta un momento (sin conexion con el servidor) y vuelve, el
// sonido y la pantalla encendida siguen como estaban, sin otro toque. Una recarga lo olvida: el navegador exige otro toque para sonar.
function browserAudio(): AudioLike | null {
  const scope = window as unknown as { AudioContext?: new () => unknown; webkitAudioContext?: new () => unknown }
  const Context = scope.AudioContext ?? scope.webkitAudioContext
  return Context ? new Context() as AudioLike : null
}
function browserScreen(): ScreenEnv {
  // Wake Lock se llama sobre su propio objeto (navigator.wakeLock.request): nunca suelto.
  const wakeLock = (navigator as unknown as { wakeLock?: ScreenEnv['wakeLock'] }).wakeLock
  return { secure: window.isSecureContext, wakeLock }
}
const shiftStatus = reactive<ShiftStatus>({ started: false, sound: 'off', screen: 'off' })
const shift = new Shift(shiftStatus, { audio: browserAudio, screen: browserScreen })
</script>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import type { Session } from '../api'
import {
  alsoText, applyShortcut, CONFLICT_TEXT, countText, DIRECT_DECISIONS, dishAlarming, dishNote, dishReadyIds, dishState, dishText, DONE_BAND, doneText, elapsedText,
  exceptionLine, listoPerDish, markLabel, missingText, nextText, noteReady, noteShortcuts, noteSpent, passDishIds, passTickets, PRESEND_DECISIONS, presendReviews,
  progressText, readyBody, readySince, REMAKE_DECISION, remakeQuestion, restBody, restButton, restDishes, restOfGuest, ringKeys, seesWholeTable, shouldChime,
  stationLine, stationTickets, TicketWatch, timeTone, versionConflict, waitingForWaiter, type NoteShortcut, type PassTicket, type PresendGuest, type PresendItem,
  type StationMark, type StationTicket,
} from '../cocina'
import { Names, useConfiguration } from '../configuration'
import { restrictionAlarming } from '../floor'
import { clockLabel, guestLabel, plural, restrictionLabel } from '../labels'
import { useOperations } from '../operations'
import { screenHint, screenSupport, shiftIntro, soundText } from '../turno'
import type { Outcome } from '../commands'
import type { BoardEntry, Course, GuestRestriction, Preparation } from '../types'
import Icon from './Icon.vue'
import StatusPanel from './StatusPanel.vue'

// Costina Cocina, C3d (plan 2026-10-27; plan 2026-10-22, seccion 4.3; maqueta 2026-10-22, pestanas «3 · Cocina · estación» y
// «4 · Cocina · pase»). Misma lectura en vivo y misma orden incierta que Sala (operations.ts, StatusPanel). La logica, sin DOM, es
// cocina.ts; aqui solo se pinta. Ningun boton sale de aqui si el servidor no anuncia su accion en ESA elaboracion o en ESE pase (ya
// filtrada por rol y estacion, D3.1, D4.3a, E4e), y cada toque es UNA orden (D6.3): «Listo» y «Marcar … como lista» son una orden
// preparations-ready; «Validar pase», una ready; cada revision, una review-preparation; «No le afecta a nada más», una review-preparations.
//  - ESTACION: solo su trabajo (ADR-017 sustituye la regla de D6.4 «una estacion ve el pase entero»): una comanda por mesa y pase, con
//    los platos agrupados, sus excepciones (alergias, nunca atenuadas), «También en: …», el tiempo y un «Listo» grande. Sin «Empezar»
//    (respuesta 1 del 22-10) y sin deshacer. Lo terminado baja a la franja «Hecho · esperando al pase». Nada que escribir.
//  - PASE (y una tablet de cocina sin estacion): a la derecha (arriba en una pantalla estrecha), «Por revisar antes de enviar» (E4e, con
//    los atajos de la nota) y «Esperando al camarero»; a la izquierda, la mesa entera con el progreso de cada estacion, que falta,
//    «Siguiente: … (sin enviar)», «Validar pase: listo para servir» y «Más opciones» con «Marcar <estación> como lista» (respuesta 2).
//  - «Empezar turno» (respuesta 4): el toque que deja sonar; con https, ademas, la pantalla encendida (turno.ts).
//  - Un version_conflict (otra pantalla en la misma mesa a la vez) relee y lo dice en la comanda: se toca otra vez. Nunca se reenvia solo.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

const { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))
const { configuration, reload } = useConfiguration(props, () => emit('unauthorized'))
const names = computed(() => new Names(configuration))
function refreshAll(): void { void live.refresh(); void reload() }

const station = computed(() => props.session.station)
const whole = computed(() => seesWholeTable(station.value))
// Estacion: la rejilla (por hacer) y la franja «Hecho».
const own = computed(() => whole.value ? [] : stationTickets(state.entries, station.value as string))
const grid = computed(() => own.value.filter(ticket => !ticket.done))
const done = computed(() => own.value.filter(ticket => ticket.done))
// Pase: las comandas, lo que espera al camarero y lo que se revisa antes de enviar.
const pass = computed(() => whole.value ? passTickets(state.entries) : [])
const waiting = computed(() => whole.value ? waitingForWaiter(state.entries) : [])
const presend = computed(() => whole.value ? presendReviews(state.entries) : [])
const count = computed(() => whole.value ? countText(pass.value.length, presend.value.length) : countText(grid.value.length, null))
const clock = computed(() => clockLabel(new Date(now.value).toISOString()))

// ---- Nombres, nunca codigos ----
const tableName = (entry: BoardEntry) => names.value.table(entry.service.tableId)
const stationName = (stationId: string) => names.value.station(stationId)
const title = (entry: BoardEntry, course: Course) => `${tableName(entry)} · ${course.name}`
const testKey = (entry: BoardEntry, course: Course) => `${entry.service.tableId}-${course.id}`
const TONES = { neutral: 't-neutral', amber: 't-amber', 'dark-amber': 't-late' } as const
const toneOf = (course: Course) => TONES[timeTone(course.firedAt, now.value)]
const lineTone = (preparation: Preparation) => exceptionLine(preparation).alarming ? 'alarm' : 'warn'
const restrictionTone = (restriction: GuestRestriction) => restrictionAlarming(restriction) ? 'alarm' : 'warn'

// ---- Ordenes: una por toque, con la version LEIDA de esa mesa (F01) ----
const conflict = ref<string | null>(null)
// Devuelve el resultado (null si no hubo orden): una revision solo borra su nota tras saberlo (noteSpent).
async function send(key: string, entry: BoardEntry, action: string, body: Record<string, unknown>, description: string): Promise<Outcome | null> {
  conflict.value = null
  const outcome = await run(`/dining/services/${encodeURIComponent(entry.service.id)}/commands/${action}`, body, description)
  if (versionConflict(outcome)) conflict.value = key
  return outcome
}
function listo(ticket: StationTicket | PassTicket, itemIds: string[], what: string): void {
  if (itemIds.length === 0) return
  void send(ticket.key, ticket.entry, 'preparations-ready', readyBody(ticket.entry, ticket.course, itemIds), `Listo: ${what} · ${title(ticket.entry, ticket.course)}`)
}
function markStation(ticket: PassTicket, mark: StationMark): void {
  void send(ticket.key, ticket.entry, 'preparations-ready', readyBody(ticket.entry, ticket.course, mark.itemIds),
    `${markLabel(mark, stationName)} · ${title(ticket.entry, ticket.course)}`)
}
function validate(ticket: PassTicket): void {
  void send(ticket.key, ticket.entry, 'ready', { expectedVersion: ticket.entry.version, courseId: ticket.course.id },
    `Validar pase: listo para servir · ${title(ticket.entry, ticket.course)}`)
}

// ---- Notas de revision (lo unico que se escribe, y solo en el pase), con sus atajos a la vista ----
const notes = reactive<Record<string, string>>({})
const restNotes = reactive<Record<string, string>>({})
// C3e.1 (usabilidad 28-10, H48): los atajos segun las restricciones que aplican a ESE plato (o al comensal, para todo lo que queda): sin
// una de gluten no sale «Sin gluten». «No le afecta a nada más» no cambia ningun plato: sin «Cambiado por …».
const restShortcuts = (guest: PresendGuest) => noteShortcuts(guest.restrictions).filter(shortcut => !shortcut.open)
function shortcut(store: Record<string, string>, key: string, fieldId: string, chosen: NoteShortcut): void {
  store[key] = applyShortcut(chosen)
  // «Cambiado por …»: el cursor al final, para escribir por que (en la tablet abre el teclado).
  if (chosen.open) void nextTick(() => {
    const field = document.getElementById(fieldId) as HTMLInputElement | null
    if (!field) return
    field.focus(); field.setSelectionRange(field.value.length, field.value.length)
  })
}
// Lo ENVIADO por revisar, en su comanda: «No le afecta», «Adaptado» o «Rehacer» (solo el pase: el servidor no se lo anuncia a nadie mas).
// Revision de C3d: cada nota se borra SOLO tras el resultado y si la orden se aplico o quedo pendiente (noteSpent). Tras un
// version_conflict sigue escrita, y «toca otra vez» es un toque. Mientras la orden va y se relee, todo esta apagado (locked): nadie escribe.
const sentKey = (ticket: PassTicket, preparation: Preparation) => `enviado:${ticket.key}:${preparation.id}`
const reviewable = (ticket: PassTicket, preparation: Preparation) => ticket.reviews.includes(preparation)
// C3e.1 (usabilidad 28-10, H53): «Rehacer» en DOS pasos, como retirar una alergia en Sala (AllergySheet.vue). El primer toque no manda
// nada: en su sitio sale la pregunta (remakeQuestion) con «Cancelar» y «Sí, rehacer», que es la orden. Una sola pregunta a la vez; la
// nota escrita se queda. El foco va a «Cancelar» y, al cancelar, vuelve a «Rehacer» (sin desplazar la pantalla).
const remaking = ref<string | null>(null)
const remakeId = (ticket: PassTicket, preparation: Preparation) => `${testKey(ticket.entry, ticket.course)}-${preparation.id}`
function focusTestId(testId: string): void {
  void nextTick(() => document.querySelector<HTMLElement>(`[data-testid="${CSS.escape(testId)}"]`)?.focus({ preventScroll: true }))
}
function askRemake(ticket: PassTicket, preparation: Preparation): void {
  remaking.value = sentKey(ticket, preparation)
  focusTestId(`remake-cancel-${remakeId(ticket, preparation)}`)
}
function cancelRemake(ticket: PassTicket, preparation: Preparation): void {
  remaking.value = null
  focusTestId(`remake-${remakeId(ticket, preparation)}`)
}
async function reviewSent(ticket: PassTicket, preparation: Preparation, decision: string, label: string): Promise<void> {
  const key = sentKey(ticket, preparation), note = (notes[key] ?? '').trim()
  if (!noteReady(note)) return
  const outcome = await send(ticket.key, ticket.entry, 'review-preparation',
    { expectedVersion: ticket.entry.version, courseId: ticket.course.id, itemId: preparation.id, decision, note },
    `Revisión (${label}) de ${preparation.name} · ${guestLabel(preparation.guestPosition)} · ${title(ticket.entry, ticket.course)}`)
  if (noteSpent(outcome)) notes[key] = ''
  // C3e.1 (H53): tras un version_conflict la pregunta de «Rehacer» sigue abierta, como la nota: «toca otra vez» es un toque. Con cualquier
  // otro resultado se cierra (si el plato vuelve a quedar por revisar, empieza otra vez por el primer paso).
  if (remaking.value === key && !versionConflict(outcome)) remaking.value = null
}
// C3e.1 (usabilidad 28-10, H53): la pregunta es de UNA revision. Si al releer ya no esta en el pase (otro puesto decidio ese plato), se
// cierra: si el plato vuelve a quedar por revisar (otra restriccion tardia del mismo comensal), empieza otra vez por el primer paso.
watch(pass, tickets => {
  if (remaking.value !== null && !tickets.some(ticket => ticket.reviews.some(preparation => sentKey(ticket, preparation) === remaking.value))) remaking.value = null
})

// ---- E4e: «Por revisar antes de enviar» ----
const guestTable = (guest: PresendGuest) => names.value.table(guest.entry.service.tableId)
const guestId = (guest: PresendGuest) => `${guest.entry.service.tableId}-${guest.guestPosition ?? 'all'}`
const itemId = (guest: PresendGuest, item: PresendItem) => `${guest.entry.service.tableId}-${item.course.id}-${item.preparation.id}`
const itemKey = (guest: PresendGuest, item: PresendItem) => `antes:${guest.entry.service.id}:${item.course.id}:${item.preparation.id}`
// E4e (revision): el id de cada campo de nota (y su etiqueta) sale de su POSICION en la lista, unico por construccion. El id de una
// elaboracion (<plato>-<comensal>) se repite entre mesas con la misma oferta: con el, la etiqueta de una mesa llevaba a la nota de otra.
const rest = computed(() => new Map(presend.value.map(guest => [guest.key, restOfGuest(guest)])))
async function presendReview(guest: PresendGuest, item: PresendItem, decision: string, label: string): Promise<void> {
  const key = itemKey(guest, item), note = (notes[key] ?? '').trim()
  if (!noteReady(note)) return
  const outcome = await send(guest.key, guest.entry, 'review-preparation',
    { expectedVersion: guest.entry.version, courseId: item.course.id, itemId: item.preparation.id, decision, note },
    `Revisión antes de enviar (${label}) de ${item.preparation.name} · ${guestLabel(guest.guestPosition)} · ${item.course.name} · ${guestTable(guest)}`)
  if (noteSpent(outcome)) notes[key] = ''
}
// C3e.1 (usabilidad 28-10, H56): su version_conflict se dice junto a su boton, arriba (restKey), no al final de la tarjeta, varios
// formularios mas abajo; el aviso de abajo queda para las decisiones de cada plato.
const restKey = (guest: PresendGuest) => `rest:${guest.key}`
async function nothingElse(guest: PresendGuest): Promise<void> {
  const note = (restNotes[guest.key] ?? '').trim(), items = rest.value.get(guest.key) ?? []
  if (!noteReady(note) || items.length === 0) return
  const outcome = await send(restKey(guest), guest.entry, 'review-preparations', restBody(guest, note),
    `No le afecta a nada más (${plural(items.length, 'plato', 'platos')}) · ${guestLabel(guest.guestPosition)} · ${guestTable(guest)}`)
  if (noteSpent(outcome)) restNotes[guest.key] = ''
}

// ---- «Empezar turno» y el sonido de una comanda nueva ----
const intro = shiftIntro(screenSupport(browserScreen()))
const shiftNote = computed(() => [soundText(shiftStatus.sound), screenHint(shiftStatus.screen)].filter(text => text.length > 0).join(' '))
const shiftWarn = computed(() => shiftStatus.sound !== 'on' || shiftStatus.screen !== 'on')
function startShift(): void { void shift.start() }
// Tras CADA lectura correcta (nunca del contenido del aviso del canal, D2): las comandas nuevas de la rejilla. La primera no suena.
const tickets = new TicketWatch()
watch(() => state.readAt, () => {
  const fresh = tickets.read(ringKeys(state.entries, station.value))
  if (shouldChime(fresh, shiftStatus.started)) shift.ring()
})
// El navegador suelta la pantalla encendida al ocultar la pagina: al volver a verse se pide otra vez.
const visible = () => { if (document.visibilityState === 'visible') void shift.awake() }
onMounted(() => { document.addEventListener('visibilitychange', visible) })
onUnmounted(() => { document.removeEventListener('visibilitychange', visible) })
</script>

<template>
  <section class="kitchen" :class="whole ? 'k-pass' : 'k-station'" data-testid="kitchen">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="refreshAll">
      <p class="k-meta"><span class="k-count" data-testid="kitchen-count">{{ count }}</span><span class="k-clock" data-testid="kitchen-clock">{{ clock }}</span></p>
    </StatusPanel>

    <!-- «Empezar turno»: no tapa las comandas (una tablet que se reinicia en pleno servicio las ensena al momento); sin el toque, no suena. -->
    <section v-if="!shiftStatus.started" class="shift" aria-label="Empezar turno" data-testid="shift">
      <p class="shift-t">{{ intro }}</p>
      <button type="button" class="btn btn-primary k-btn shift-go" data-testid="do-shift-start" @click="startShift"><Icon name="play" :size="22" />Empezar turno</button>
    </section>
    <p v-else class="shift-on" :class="{ warn: shiftWarn }" role="status" data-testid="shift-on"><Icon name="bell" :size="20" />{{ shiftNote }}</p>

    <!-- ===================== ESTACION: solo lo suyo ===================== -->
    <template v-if="!whole">
      <p v-if="state.readAt && grid.length === 0" class="k-empty" data-testid="kitchen-empty"><Icon name="chef" :size="24" />No hay comandas. Las nuevas aparecen aquí solas.</p>
      <ul class="k-grid">
        <li v-for="ticket in grid" :key="ticket.key" class="tk" :data-testid="`ticket-${testKey(ticket.entry, ticket.course)}`">
          <header class="tk-h">
            <h3 class="tk-name">{{ title(ticket.entry, ticket.course) }}</h3>
            <div class="tk-s2">
              <span>{{ plural(ticket.entry.service.pax, 'persona', 'personas') }}</span>
              <span class="tmr" :class="toneOf(ticket.course)" data-testid="elapsed"><Icon name="clock" :size="18" :stroke="2.4" />{{ elapsedText(ticket.course.firedAt, now) }}</span>
            </div>
          </header>
          <p v-if="ticket.paused" class="k-paused" role="status"><Icon name="pause" :size="18" />Mesa en pausa</p>
          <ul v-if="ticket.wholeTable.length > 0" class="k-whole">
            <li v-for="restriction in ticket.wholeTable" :key="restriction.id" class="k-al" :class="restrictionTone(restriction)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ restrictionLabel(restriction) }}</span></li>
          </ul>
          <div class="tk-b">
            <div v-for="(dish, d) in ticket.dishes" :key="dish.key" class="dg" :class="{ done: dish.ready }" :data-testid="`dish-${testKey(ticket.entry, ticket.course)}-${d}`">
              <p class="dg-n"><b>{{ dish.name }}</b><span class="dg-qr"><span class="dg-q">× {{ dish.quantity }}</span><span v-if="dishNote(dish)" class="dg-x" :class="{ alarm: dishAlarming(dish) }">{{ dishNote(dish) }}</span></span></p>
              <span v-if="dish.ready" class="dg-ok"><Icon name="check" :size="20" :stroke="2.8" />Lista</span>
              <button v-else-if="listoPerDish(ticket) && dishReadyIds(ticket.course, dish).length > 0" type="button" class="listo" :disabled="locked"
                :aria-label="`Listo: ${dishText(dish)}`" :data-testid="`do-listo-${testKey(ticket.entry, ticket.course)}-${d}`"
                @click="listo(ticket, dishReadyIds(ticket.course, dish), dishText(dish))">Listo</button>
              <!-- C3e.1 (usabilidad 28-10, H34): lo por revisar dice que hacer («… · no lo emplates hasta que decida»): stationLine. -->
              <ul v-if="dish.exceptions.length > 0" class="k-als">
                <li v-for="preparation in dish.exceptions" :key="preparation.id" class="k-al" :class="lineTone(preparation)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ stationLine(preparation).text }}</span></li>
              </ul>
            </div>
            <p v-if="ticket.alsoIn.length > 0" class="also" :data-testid="`also-${testKey(ticket.entry, ticket.course)}`"><Icon name="right" :size="18" :stroke="2.2" />{{ alsoText(ticket.alsoIn, stationName) }}</p>
          </div>
          <p v-if="conflict === ticket.key" class="k-conflict" role="status" :data-testid="`conflict-${testKey(ticket.entry, ticket.course)}`"><Icon name="info" :size="20" />{{ CONFLICT_TEXT }}</p>
          <div v-if="ticket.readyIds.length > 0" class="tk-f">
            <button type="button" class="todo" :disabled="locked" :aria-label="`Listo: ${title(ticket.entry, ticket.course)}`"
              :data-testid="`do-listo-${testKey(ticket.entry, ticket.course)}`" @click="listo(ticket, ticket.readyIds, doneText(ticket))"><Icon name="check" :size="24" :stroke="2.8" />Listo</button>
          </div>
        </li>
      </ul>

      <!-- Lo suyo ya terminado, hasta que el pase valida. Sin hora (el motor no la guarda) y sin deshacer. Las alergias, tambien aqui.
           C3e.1 (usabilidad 28-10, H34): aqui la linea por revisar NO dice «no lo emplates hasta que decida» (exceptionLine, no stationLine):
           el plato ya esta marcado Listo, y esa orden haria creer que hay que retirarlo. Sigue en rojo si alarma y escrita entera. -->
      <section class="done-band" aria-label="Hecho · esperando al pase" data-testid="done-band">
        <h2 class="db-l"><Icon name="check" :size="20" :stroke="2.8" />{{ DONE_BAND }}</h2>
        <p v-if="done.length === 0" class="db-empty">Nada</p>
        <ul v-else class="db-list">
          <li v-for="ticket in done" :key="ticket.key" class="hrow" :data-testid="`done-${testKey(ticket.entry, ticket.course)}`">
            <Icon name="check" :size="20" :stroke="2.8" />
            <div class="hr-t">
              <p><b>{{ title(ticket.entry, ticket.course) }}</b> · {{ doneText(ticket) }}</p>
              <p v-for="restriction in ticket.wholeTable" :key="restriction.id" class="k-al" :class="restrictionTone(restriction)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ restrictionLabel(restriction) }}</span></p>
              <template v-for="dish in ticket.dishes" :key="dish.key">
                <p v-for="preparation in dish.exceptions" :key="preparation.id" class="k-al" :class="lineTone(preparation)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ dish.name }} · {{ exceptionLine(preparation).text }}</span></p>
              </template>
            </div>
          </li>
        </ul>
      </section>
    </template>

    <!-- ===================== PASE: la mesa entera ===================== -->
    <div v-else class="pase-body">
      <aside class="pside">
        <!-- E4e: lo que el pase decide antes de enviar, siempre arriba. Tipo, sustancia y gravedad escritos; nunca atenuado. -->
        <section class="side-sec side-presend" aria-labelledby="presend-title">
          <h2 id="presend-title" class="side-h"><Icon name="alert" :size="22" :stroke="2.2" />Por revisar antes de enviar<span class="cnt">{{ presend.length }}</span></h2>
          <p v-if="presend.length === 0" class="side-hint" data-testid="presend-empty">Nada por revisar.</p>
          <div v-else class="presend" data-testid="presend">
            <p class="side-hint">Decide cada plato antes de que salga su pase: hasta entonces, ese pase espera.</p>
            <article v-for="(guest, g) in presend" :key="guest.key" class="rv-c" :class="{ warn: !guest.restrictions.some(restrictionAlarming) }" :data-testid="`presend-${guestId(guest)}`">
              <div class="rv-t"><h3>{{ guestTable(guest) }} · {{ guestLabel(guest.guestPosition) }}</h3><span class="pill s-pending">Sin enviar</span></div>
              <p v-for="restriction in guest.restrictions" :key="restriction.id" class="k-al" :class="restrictionTone(restriction)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ restrictionLabel(restriction) }}</span></p>
              <!-- C3e.1 (usabilidad 28-10, H56): «No le afecta a nada más» ARRIBA, justo bajo la alergia: lo normal es adaptar uno o dos platos
                   y dejar el resto, y antes habia que pasar todos los formularios para llegar a el. Lo mismo que exigia: nota obligatoria y
                   solo con mas de un plato del comensal. -->
              <div v-if="guest.items.length > 1 && (rest.get(guest.key)?.length ?? 0) > 0" class="review rest" :data-testid="`presend-rest-${guestId(guest)}`">
                <label class="lbl" :for="`presend-rest-note-${g}`">Nota para todo lo que queda (obligatoria)</label>
                <input :id="`presend-rest-note-${g}`" v-model="restNotes[guest.key]" :name="`presend-rest-note-${g}`" maxlength="200" autocomplete="off" :disabled="locked" />
                <div class="k-shortcuts">
                  <button v-for="option in restShortcuts(guest)" :key="option.label" type="button" class="k-chip" :disabled="locked"
                    @click="shortcut(restNotes, guest.key, `presend-rest-note-${g}`, option)">{{ option.label }}</button>
                </div>
                <!-- C3e.1 (H56): encima del boton, enteros y sin plegar, los platos que decide; el boton los cuenta. Su version_conflict, aqui. -->
                <p class="rest-dishes" :data-testid="`presend-rest-dishes-${guestId(guest)}`">{{ restDishes(rest.get(guest.key) ?? []) }}</p>
                <button type="button" class="btn k-btn wide" :disabled="locked || !noteReady(restNotes[guest.key] ?? '')" :data-testid="`do-presend-rest-${guestId(guest)}`"
                  @click="nothingElse(guest)"><Icon name="checkCircle" :size="22" />{{ restButton(rest.get(guest.key)?.length ?? 0) }}</button>
                <p v-if="conflict === restKey(guest)" class="k-conflict" role="status" :data-testid="`conflict-rest-${guestId(guest)}`"><Icon name="info" :size="20" />{{ CONFLICT_TEXT }}</p>
              </div>
              <ul class="rv-list">
                <li v-for="(item, i) in guest.items" :key="itemKey(guest, item)" class="review presend-item" :data-testid="`presend-item-${itemId(guest, item)}`">
                  <p class="rv-d"><small>{{ item.course.name }} · {{ stationName(item.preparation.stationId) }}</small>
                    <b>{{ item.preparation.name }}<template v-if="item.preparation.quantity > 1"> × {{ item.preparation.quantity }}</template></b></p>
                  <label class="lbl" :for="`presend-note-${g}-${i}`">Nota (obligatoria)</label>
                  <input :id="`presend-note-${g}-${i}`" v-model="notes[itemKey(guest, item)]" :name="`presend-note-${g}-${i}`" maxlength="200" autocomplete="off" :disabled="locked" />
                  <div class="k-shortcuts">
                    <button v-for="option in noteShortcuts(item.preparation.restrictions ?? [])" :key="option.label" type="button" class="k-chip" :disabled="locked"
                      @click="shortcut(notes, itemKey(guest, item), `presend-note-${g}-${i}`, option)">{{ option.label }}</button>
                  </div>
                  <!-- Antes de enviar no hay «Rehacer» (nothing_to_remake): las dos, a lo ancho y sin partirse (C3e.1, H53). -->
                  <div class="row">
                    <button v-for="option in PRESEND_DECISIONS" :key="option.decision" type="button" class="btn k-btn" :class="{ 'btn-primary': option.decision === 'adapt' }"
                      :disabled="locked || !noteReady(notes[itemKey(guest, item)] ?? '')" :data-testid="`do-presend-${option.decision}-${itemId(guest, item)}`"
                      @click="presendReview(guest, item, option.decision, option.label)">{{ option.label }}</button>
                  </div>
                </li>
              </ul>
              <p v-if="conflict === guest.key" class="k-conflict" role="status"><Icon name="info" :size="20" />{{ CONFLICT_TEXT }}</p>
            </article>
          </div>
        </section>

        <!-- Validado y sin servir. Nunca «Sala avisada»: el servidor no sabe si el aviso llego a un movil (D2). Revision de C3d: en la
             tablet, siempre a la vista al pie de la columna, aunque haya mucho por revisar encima (style.css, .side-waiting). -->
        <section class="side-sec side-waiting" aria-labelledby="waiting-title" data-testid="waiting">
          <h2 id="waiting-title" class="side-h"><Icon name="check" :size="22" :stroke="2.6" />Esperando al camarero<span class="cnt">{{ waiting.length }}</span></h2>
          <p v-if="waiting.length === 0" class="side-hint">Nada esperando.</p>
          <ul v-else class="wt">
            <li v-for="item in waiting" :key="item.key" class="wt-r" :data-testid="`waiting-${testKey(item.entry, item.course)}`">
              <Icon name="check" :size="22" :stroke="2.8" /><span><b>{{ title(item.entry, item.course) }}</b> · {{ readySince(item.course.readyAt, now) || 'listo' }}</span>
            </li>
          </ul>
          <p class="side-hint">Se van solas cuando el camarero pulsa «Servido».</p>
        </section>
      </aside>

      <div class="pmain">
        <p v-if="state.readAt && pass.length === 0" class="k-empty" data-testid="kitchen-empty"><Icon name="chef" :size="24" />No hay comandas en cocina. Las nuevas aparecen aquí solas.</p>
        <ul class="pcards">
          <li v-for="(ticket, t) in pass" :key="ticket.key" class="pc" :class="{ all: ticket.canValidate }" :data-testid="`ticket-${testKey(ticket.entry, ticket.course)}`">
            <header class="tk-h">
              <h3 class="tk-name">{{ title(ticket.entry, ticket.course) }}</h3>
              <div class="tk-s2">
                <span>{{ plural(ticket.entry.service.pax, 'persona', 'personas') }}</span>
                <span class="tmr" :class="toneOf(ticket.course)" data-testid="elapsed"><Icon name="clock" :size="18" :stroke="2.4" />{{ elapsedText(ticket.course.firedAt, now) }}</span>
              </div>
            </header>
            <p v-if="ticket.paused" class="k-paused" role="status"><Icon name="pause" :size="18" />Mesa en pausa</p>
            <ul class="pc-st" :data-testid="`progress-${testKey(ticket.entry, ticket.course)}`">
              <li v-for="row in ticket.progress" :key="row.stationId" class="schip" :class="{ ok: row.done }"><Icon v-if="row.done" name="check" :size="18" :stroke="3" />{{ progressText(row, stationName) }}</li>
            </ul>
            <ul v-if="ticket.wholeTable.length > 0" class="k-whole">
              <li v-for="restriction in ticket.wholeTable" :key="restriction.id" class="k-al" :class="restrictionTone(restriction)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ restrictionLabel(restriction) }}</span></li>
            </ul>
            <ul class="pd-l">
              <li v-for="(dish, d) in ticket.dishes" :key="dish.key" class="pd" :class="{ ok: dish.ready }" :data-testid="`dish-${testKey(ticket.entry, ticket.course)}-${d}`">
                <div class="pd-r">
                  <span class="pd-s"><Icon v-if="dish.ready" name="check" :size="22" :stroke="3" /><i v-else class="todo-dot" aria-hidden="true"></i></span>
                  <p class="pd-n"><b>{{ dish.name }}</b> <span class="pd-q">× {{ dish.quantity }}</span><span v-if="dishNote(dish)" class="pd-x" :class="{ alarm: dishAlarming(dish) }">{{ dishNote(dish) }}</span>
                    <span v-if="ticket.progress.length > 1" class="pd-at">{{ stationName(dish.stationId) }}</span></p>
                  <button v-if="passDishIds(ticket, dish).length > 0" type="button" class="listo" :disabled="locked" :aria-label="`Listo: ${dishText(dish)}`"
                    :data-testid="`do-listo-${testKey(ticket.entry, ticket.course)}-${d}`" @click="listo(ticket, passDishIds(ticket, dish), dishText(dish))">Listo</button>
                  <span v-else class="pd-st">{{ dishState(dish) }}</span>
                </div>
                <template v-for="(preparation, e) in dish.exceptions" :key="preparation.id">
                  <p class="k-al pd-al" :class="lineTone(preparation)"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ exceptionLine(preparation).text }}</span></p>
                  <div v-if="reviewable(ticket, preparation)" class="review k-review" :data-testid="`review-${testKey(ticket.entry, ticket.course)}-${preparation.id}`">
                    <label class="lbl" :for="`review-note-${t}-${d}-${e}`">Nota (obligatoria)</label>
                    <input :id="`review-note-${t}-${d}-${e}`" v-model="notes[sentKey(ticket, preparation)]" :name="`review-note-${t}-${d}-${e}`" maxlength="200" autocomplete="off" :disabled="locked" />
                    <div class="k-shortcuts">
                      <button v-for="option in noteShortcuts(preparation.restrictions ?? [])" :key="option.label" type="button" class="k-chip" :disabled="locked"
                        @click="shortcut(notes, sentKey(ticket, preparation), `review-note-${t}-${d}-${e}`, option)">{{ option.label }}</button>
                    </div>
                    <!-- C3e.1 (usabilidad 28-10, H53): «No le afecta» y «Adaptado», a lo ancho y sin partirse; «Rehacer», en su fila, con borde
                         de aviso y en dos pasos: el primer toque pregunta y solo «Sí, rehacer» (do-review-remake-…) manda la orden. -->
                    <div class="row">
                      <button v-for="option in DIRECT_DECISIONS" :key="option.decision" type="button" class="btn k-btn" :class="{ 'btn-primary': option.decision === 'adapt' }"
                        :disabled="locked || !noteReady(notes[sentKey(ticket, preparation)] ?? '')"
                        :data-testid="`do-review-${option.decision}-${testKey(ticket.entry, ticket.course)}-${preparation.id}`"
                        @click="reviewSent(ticket, preparation, option.decision, option.label)">{{ option.label }}</button>
                    </div>
                    <button v-if="remaking !== sentKey(ticket, preparation)" type="button" class="btn k-btn k-remake"
                      :disabled="locked || !noteReady(notes[sentKey(ticket, preparation)] ?? '')" :data-testid="`remake-${remakeId(ticket, preparation)}`"
                      @click="askRemake(ticket, preparation)"><Icon name="refresh" :size="22" :stroke="2.2" />{{ REMAKE_DECISION.label }}</button>
                    <div v-else class="k-remake-ask" :data-testid="`remake-step-${remakeId(ticket, preparation)}`">
                      <p class="k-remake-q">{{ remakeQuestion(preparation) }}</p>
                      <div class="row">
                        <button type="button" class="btn k-btn" :data-testid="`remake-cancel-${remakeId(ticket, preparation)}`"
                          @click="cancelRemake(ticket, preparation)">Cancelar</button>
                        <button type="button" class="btn k-btn k-remake" :disabled="locked || !noteReady(notes[sentKey(ticket, preparation)] ?? '')"
                          :data-testid="`do-review-${REMAKE_DECISION.decision}-${remakeId(ticket, preparation)}`"
                          @click="reviewSent(ticket, preparation, REMAKE_DECISION.decision, REMAKE_DECISION.label)"><Icon name="refresh" :size="22" :stroke="2.2" />Sí, rehacer</button>
                      </div>
                    </div>
                  </div>
                </template>
              </li>
            </ul>
            <p v-if="ticket.next" class="pc-next" :data-testid="`next-${testKey(ticket.entry, ticket.course)}`">{{ nextText(ticket.next) }}</p>
            <p v-if="conflict === ticket.key" class="k-conflict" role="status" :data-testid="`conflict-${testKey(ticket.entry, ticket.course)}`"><Icon name="info" :size="20" />{{ CONFLICT_TEXT }}</p>
            <div class="pc-f">
              <!-- Solo cuando el servidor anuncia validar. Ese toque es la llamada al camarero. -->
              <button v-if="ticket.canValidate" type="button" class="pase-go" :disabled="locked" :data-testid="`do-ready-${testKey(ticket.entry, ticket.course)}`"
                @click="validate(ticket)"><Icon name="bell" :size="24" :stroke="2.2" />Validar pase: listo para servir</button>
              <p v-else-if="missingText(ticket, stationName)" class="pc-miss" :data-testid="`missing-${testKey(ticket.entry, ticket.course)}`"><Icon name="clock" :size="20" :stroke="2.2" />{{ missingText(ticket, stationName) }}</p>
              <details v-if="ticket.marks.length > 0" class="more k-more" :data-testid="`more-${testKey(ticket.entry, ticket.course)}`">
                <summary><Icon name="dots" :stroke="2.6" /><span>Más opciones</span><Icon name="down" /></summary>
                <div class="more-b">
                  <!-- Respuesta 2 del 22-10: el pase marca por una estacion (con la tablet estropeada), solo lo que el servidor le deja. -->
                  <button v-for="mark in ticket.marks" :key="mark.stationId" type="button" class="btn k-btn" :disabled="locked"
                    :data-testid="`do-mark-${testKey(ticket.entry, ticket.course)}-${mark.stationId}`" @click="markStation(ticket, mark)"><Icon name="check" :size="22" :stroke="2.6" />{{ markLabel(mark, stationName) }}</button>
                </div>
              </details>
            </div>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>
