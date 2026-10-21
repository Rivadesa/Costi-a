<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { addProblem, HAD_EMPTY, HAD_FOOT, HAD_SUB, HAD_TITLE, hadDetail, leaveText, ROUND_SENT, ROUND_UNCONFIRMED, roundNames, roundProblem, VOIDED } from '../bodega'
import type { CatalogStatus } from '../catalog'
import { offerFor, type Configuration, type Names } from '../configuration'
import type { DrinkProduct, RoundItem } from '../drinks'
import { commandFor, coursesTitle, dishesHint, PENDING_REVIEW_TEXT, primaryAction, REASON_PROBLEM, reasonLabel, reasonOk, serviceCommand, tableActions, tableInfo, waitingText, type TableAction } from '../ficha'
import { courseDetail, courseStep, isTileCode, restrictionAlarming, tileLabel, tileState } from '../floor'
import type { IconName } from '../icons'
import { clockLabel, courseLabel, courseStateLabel, currentCourse, guestLabel, offerKindLabel, plural, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { Round, type Settlement } from '../round'
import type { BoardEntry, Course, GuestRestriction } from '../types'
import DishPanel from './DishPanel.vue'
import DrinksPanel from './DrinksPanel.vue'
import Icon from './Icon.vue'
import LineSheet from './LineSheet.vue'
import RoundTray from './RoundTray.vue'

// C3a: ficha de una mesa en el comandero, como la maqueta aprobada («1 · Mesa»). Arriba las restricciones, en rojo y ESCRITAS; dos
// entradas grandes, «Carta y menús» (elegir y pedir platos, lo de hoy) y «Bodega y bebidas» (C3b: buscador, Copa o Botella y la
// ronda); los pases en corto; y abajo UN boton grande con la primera accion que anuncia el servidor, en el orden del Comedor. Todo
// lo demas anunciado va en «Más opciones». Ninguna regla de negocio: cada boton existe porque el servidor lo anuncia y cada orden sale
// de ESTA lectura (F01), por la orden incierta de siempre (operations.ts). Lo unico local es el formato de lo tecleado (motivo,
// sustancia, cantidad).
// C3b: «Bodega y bebidas» rehecha (DrinksPanel), la RONDA de esta mesa (un borrador en memoria, de la ficha: sigue al ir y volver de
// Bebidas y se ve abajo tambien en la ficha) y «Lo que lleva la mesa», sin importes (ADR-007, apartado C3b). La ronda sale en UNA orden
// add-consumptions por sendRound (la orden incierta de siempre) con la version de ESTA lectura (F01). Salir de la mesa con una ronda sin
// enviar pide confirmacion: no se guarda en ningun sitio. Una ronda sin confirmar ya es la orden incierta: la bandeja lo dice y la
// pantalla sube a su panel; con una orden incierta o bloqueada (uncertain) la bandeja explica por que «Enviar» no se puede tocar.
const props = defineProps<{
  entry: BoardEntry; tableName: string; roomName: string; configuration: Configuration; names: Names
  products: DrinkProduct[]; catalogStatus: CatalogStatus; canConsume: boolean; locked: boolean; uncertain: boolean; now: number
  sendRound: (path: string, body: Record<string, unknown>, description: string) => Promise<Settlement>
}>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string]; back: []; bodega: [] }>()

const service = computed(() => props.entry.service)
const has = (action: string) => service.value.actions?.includes(action) === true
const panel = ref<'ficha' | 'carta' | 'bodega'>('ficha')
watch(() => service.value.id, () => { panel.value = 'ficha'; problem.value = '' })
watch(panel, () => { window.scrollTo({ top: 0 }) })

// ---- Cabecera ----
const state = computed(() => tileState(service.value))
const tone = computed(() => isTileCode(state.value) ? `t-${state.value}` : 't-other')
// La oferta de la mesa, aunque ya no sea vigente (un menu de comida despues de las 17:00): sus platos siguen valiendo para esta mesa.
const offer = computed(() => offerFor(props.configuration, service.value.offerId))
const roomAndOffer = computed(() => [props.roomName, offer.value?.name ?? ''].filter(part => part).join(' · '))
// «4 personas · en cocina desde las 21:14 (12 min)»: la linea corta antes de la hora, nunca deja «min)» sola en un movil.
const info = computed(() => {
  const text = tableInfo(service.value, props.now), cut = text.lastIndexOf(' (')
  return cut < 0 ? { head: text, tail: '' } : { head: text.slice(0, cut), tail: text.slice(cut) }
})
const PANEL_TITLES = { carta: 'Carta y menús', bodega: 'Bodega y bebidas' } as const
// En Bebidas, como en la maqueta: «Mesa 4 · 4 personas» bajo el titulo, sin el estado de la mesa.
const bodegaSub = computed(() => `${props.tableName} · ${plural(service.value.pax, 'persona', 'personas')}`)

// ---- Bodega y bebidas: la ronda (borrador en memoria, round.ts) ----
const round = reactive(new Round()) as Round
const trayOpen = ref(false)
const addMessage = ref('')
const sent = ref('')
const editing = ref<number | null>(null)
const leaving = ref(false)
const editLine = computed(() => editing.value === null ? null : round.lines[editing.value] ?? null)
const roundMessage = computed(() => roundProblem(round.problem, round.lines) || addMessage.value)
const imageOf = (productId: string) => props.products.find(product => product.id === productId)?.imageSha ?? null
watch(() => round.lines.length, count => { if (count === 0) { trayOpen.value = false; editing.value = null } })
// El aviso de un toque que no sumo (30 lineas, 99 de una) deja de valer en cuanto la ronda cambia.
watch(() => [round.units, round.lines.length], () => { addMessage.value = '' })
// «Sin confirmar» solo mientras haya una orden incierta: resuelta (o descartada), la bandeja vacia vuelve a decir lo de siempre.
watch(() => props.uncertain, uncertain => { if (!uncertain && sent.value === ROUND_UNCONFIRMED) sent.value = '' })

function openBodega(): void { panel.value = 'bodega'; emit('bodega') }   // el catalogo se revalida al abrir Bebidas (ETag)
function addToRound(item: RoundItem): void {
  const result = round.add(item)
  addMessage.value = addProblem(result)
  if (result === 'added' || result === 'more') sent.value = ''
}
async function sendRound(): Promise<void> {
  addMessage.value = ''
  const request = round.begin(props.entry.version, props.tableName)
  if (request === null) return
  const settlement = await props.sendRound(serviceCommand(service.value, 'add-consumptions'), request.body, request.description)
  round.settle(settlement)
  if (settlement.kind === 'confirmed') sent.value = ROUND_SENT
  // Sin confirmar: la ronda ya es la orden de arriba («Orden sin confirmar»); la bandeja vacia lo dice y la pantalla sube hasta ella.
  if (settlement.kind === 'unconfirmed') { sent.value = ROUND_UNCONFIRMED; window.scrollTo({ top: 0 }) }
  // La linea que el servidor rechaza se ensena: la lista de la ronda se abre.
  if (settlement.kind === 'rejected' && settlement.line !== null && settlement.line !== undefined) trayOpen.value = true
}
function editDone(quantity: number, guestPosition: number | null): void {
  const index = editing.value
  editing.value = null
  if (index === null) return
  round.setQuantity(index, quantity)
  round.setGuest(index, guestPosition)
}
function editRemove(): void {
  const index = editing.value
  editing.value = null
  if (index !== null) round.remove(index)
}
// Volver a Mesas: con una ronda sin enviar, primero se pregunta (es un camino excepcional; la ronda no se guarda en ningun sitio).
// Mientras la ronda va camino del servidor no hay nada que perder: lo que pase con ella se ve arriba (aviso u orden sin confirmar).
function leave(): void { if (round.lines.length > 0 && !round.sending) leaving.value = true; else emit('back') }
const stay = ref<HTMLButtonElement | null>(null)
watch(leaving, open => { if (open) void nextTick(() => stay.value?.focus()) })
function discardAndLeave(): void { leaving.value = false; round.clear(); emit('back') }

// ---- Lo que lleva la mesa: bebidas y extras ya enviados (tambien los de Caja), sin importes ni totales ----
const had = computed(() => props.entry.consumptions ?? [])

// ---- Restricciones: siempre escritas; el rojo solo cuando alarman (alergia, grave, desconocida o sin revisar por cocina). ----
const restrictions = computed<GuestRestriction[]>(() => service.value.restrictions ?? [])
const alarming = computed(() => service.value.restrictionsPendingAck === true || restrictions.value.some(restrictionAlarming))
const restrictionTone = computed(() => alarming.value ? '' : restrictions.value.length > 0 ? 'warn' : 'none')
const guests = computed(() => Array.from({ length: service.value.pax }, (_, index) => index + 1))
const KINDS = [{ code: 'Allergy', label: 'Alergia' }, { code: 'Intolerance', label: 'Intolerancia' }, { code: 'Preference', label: 'Preferencia' }] as const
const SEVERITIES = [{ code: 'Severe', label: 'Grave' }, { code: 'Moderate', label: 'Moderada' }, { code: 'Mild', label: 'Leve' }] as const
// «Toda la mesa» es una opcion mas, nunca la de por defecto (maqueta): hay que decir para quien es.
const guest = ref<number | null | undefined>(undefined)
const kind = ref<string>('Allergy')
const severity = ref<string>('Severe')
const substance = ref('')

// ---- Acciones ----
const problem = ref('')
const reason = ref('')
const actions = computed(() => tableActions(props.entry))
const primary = computed(() => primaryAction(actions.value))
const secondary = computed(() => actions.value.filter(action => action !== primary.value))
const canRemove = computed(() => has('remove-restriction') && restrictions.value.length > 0)
// Un solo campo de motivo: junto al boton grande si este lo pide; si no, en «Más opciones».
const reasonInBar = computed(() => primary.value?.needsReason === true)
const reasonInMore = computed(() => !reasonInBar.value && (secondary.value.some(action => action.needsReason) || canRemove.value))
const reasonText = computed(() => reasonLabel([...actions.value.map(action => action.action), ...(canRemove.value ? ['remove-restriction'] : [])]))
const hasMore = computed(() => secondary.value.length > 0 || canRemove.value)
const more = ref(false)
const ICON: Record<string, IconName> = {
  serve: 'check', ready: 'checkCircle', 'fire-next': 'send', start: 'play', resume: 'play', complete: 'flag', release: 'door',
  pause: 'pause', skip: 'skip', 'cancel-unstarted': 'x',
}

function send(path: string, body: Record<string, unknown>, description: string): void { problem.value = ''; emit('run', path, body, description) }
function act(action: TableAction): void {
  problem.value = ''
  if (action.needsReason && !reasonOk(reason.value)) { problem.value = REASON_PROBLEM; if (!reasonInBar.value) more.value = true; return }
  const request = commandFor(props.entry, action, props.tableName, reason.value)
  send(request.path, request.body, request.description)
  if (action.needsReason) reason.value = ''
}
function remove(restriction: GuestRestriction): void {
  if (!reasonOk(reason.value)) { problem.value = REASON_PROBLEM; more.value = true; return }
  send(serviceCommand(service.value, 'remove-restriction'), { expectedVersion: props.entry.version, restrictionId: restriction.id, reason: reason.value.trim() },
    `Retirar restricción · ${restrictionLabel(restriction)} · ${props.tableName}`)
  reason.value = ''
}
const declareProblem = ref('')
function declare(): void {
  declareProblem.value = ''
  const text = substance.value.trim()
  if (guest.value === undefined) { declareProblem.value = 'Elige para quién es: un comensal o toda la mesa.'; return }
  if (text.length < 2) { declareProblem.value = 'Escribe a qué (2 caracteres o más).'; return }
  send(serviceCommand(service.value, 'declare-restriction'), { expectedVersion: props.entry.version, guestPosition: guest.value, kind: kind.value, substance: text, severity: severity.value },
    `Declarar restricción · ${guestLabel(guest.value)} · ${text} · ${props.tableName}`)
  substance.value = ''; guest.value = undefined
}

// ---- Pases en corto ----
const current = computed(() => currentCourse(service.value))
const courseState = (course: Course) => course.state === 'Skipped' ? courseLabel(course.state) : courseStateLabel(course, service.value.pax)
function pillTone(course: Course): string {
  const step = courseStep(course)
  if (step === 'pending') return courseState(course) === 'Falta elegir plato' ? 's-choose' : 's-pending'
  return `s-${step}`
}
// Las elaboraciones se ven en lo que esta en cocina o listo (y en un estado que no se conoce): lo demas va resumido en su linea.
const showPreparations = (course: Course) => course.preparations.length > 0 && ['kitchen', 'ready', 'other'].includes(courseStep(course))
</script>

<template>
  <article class="ficha stack" :class="{ 'ficha-main': panel === 'ficha' }" data-testid="detail">
    <header class="appbar">
      <button v-if="panel === 'ficha'" type="button" class="back" data-testid="back" @click="leave"><Icon name="back" :size="24" :stroke="2.2" /><span class="back-t">Mesas</span></button>
      <button v-else type="button" class="back" data-testid="back" @click="panel = 'ficha'"><Icon name="back" :size="24" :stroke="2.2" /><span class="back-t">{{ tableName }}</span></button>
      <div class="appbar-t">
        <div class="appbar-title">
          <h2>{{ panel === 'ficha' ? tableName : PANEL_TITLES[panel] }}</h2>
          <span v-if="panel !== 'bodega'" class="pill" :class="tone" data-testid="detail-state">{{ tileLabel(state) }}</span>
        </div>
        <p v-if="panel === 'bodega'" class="appbar-sub" data-testid="detail-info">{{ bodegaSub }}</p>
        <template v-else>
          <p v-if="roomAndOffer" class="appbar-sub">{{ roomAndOffer }}</p>
          <p class="appbar-sub" data-testid="detail-info">{{ info.head }}<span v-if="info.tail" class="nw">{{ info.tail }}</span></p>
        </template>
      </div>
    </header>

    <!-- Las restricciones, arriba y escritas: quien, que y gravedad. Tambien dentro de Carta y Bodega (alli sin el formulario). -->
    <section v-if="panel === 'ficha' || restrictions.length > 0 || service.restrictionsPendingAck" class="alert-box" :class="restrictionTone"
      aria-label="Restricciones de los comensales" data-testid="restrictions">
      <p class="alert-h"><Icon :name="restrictions.length > 0 || service.restrictionsPendingAck ? 'alert' : 'checkCircle'" :size="18" :stroke="2.2" />
        {{ restrictions.length > 0 ? 'Restricciones de los comensales' : 'Sin restricciones declaradas' }}</p>
      <ul v-if="restrictions.length > 0" class="achips">
        <li v-for="restriction in restrictions" :key="restriction.id" class="achip" :class="{ strong: restrictionAlarming(restriction) }">{{ restrictionLabel(restriction) }}</li>
      </ul>
      <p v-if="service.restrictionsPendingAck" class="alert-f" data-testid="restrictions-pending">{{ PENDING_REVIEW_TEXT }}</p>
      <details v-if="panel === 'ficha' && has('declare-restriction')" class="declare" data-testid="declare">
        <summary><Icon name="plus" :stroke="2.2" />Anotar alergia o intolerancia</summary>
        <div class="stack">
          <div class="field"><span class="lbl">¿Para quién?</span>
            <div class="seg" role="radiogroup" aria-label="Para quién">
              <button v-for="position in guests" :key="position" type="button" role="radio" :aria-checked="guest === position" :aria-label="guestLabel(position)" @click="guest = position">{{ position }}</button>
              <button type="button" role="radio" :aria-checked="guest === null" @click="guest = null">Toda la mesa</button>
            </div>
          </div>
          <div class="field"><span class="lbl">Tipo</span>
            <div class="seg" role="radiogroup" aria-label="Tipo">
              <button v-for="option in KINDS" :key="option.code" type="button" role="radio" :aria-checked="kind === option.code" @click="kind = option.code">{{ option.label }}</button>
            </div>
          </div>
          <div class="field"><label for="substance">¿A qué?</label>
            <input id="substance" v-model="substance" name="substance" maxlength="80" autocomplete="off" placeholder="Por ejemplo, gluten" :disabled="locked" />
          </div>
          <div class="field"><span class="lbl">Gravedad</span>
            <div class="seg" role="radiogroup" aria-label="Gravedad">
              <button v-for="option in SEVERITIES" :key="option.code" type="button" role="radio" :aria-checked="severity === option.code" @click="severity = option.code">{{ option.label }}</button>
            </div>
          </div>
          <p v-if="declareProblem" class="notice" role="alert" data-testid="declare-problem">{{ declareProblem }}</p>
          <button type="button" class="btn btn-primary wide" :disabled="locked" data-testid="do-declare-restriction" @click="declare">Anotar restricción</button>
        </div>
      </details>
    </section>

    <template v-if="panel === 'ficha'">
      <div class="entries">
        <button type="button" class="entry" data-testid="entry-carta" @click="panel = 'carta'">
          <span class="entry-ic"><Icon name="fork" :size="26" /></span>
          <span class="entry-tx"><span class="entry-t">Carta y menús</span><span class="entry-s">{{ dishesHint(service, offer?.name ?? '') || 'Platos de la mesa' }}</span></span>
          <span class="entry-go"><Icon name="right" :size="22" /></span>
        </button>
        <button type="button" class="entry" data-testid="entry-bodega" @click="openBodega">
          <span class="entry-ic"><Icon name="glass" :size="26" /></span>
          <span class="entry-tx"><span class="entry-t">Bodega y bebidas</span><span class="entry-s">Vinos, copas, cervezas, refrescos y cafés</span></span>
          <span class="entry-go"><Icon name="right" :size="22" /></span>
        </button>
      </div>

      <section class="card courses-card" data-testid="courses">
        <div class="card-h"><h3>{{ coursesTitle(service) }}</h3><span v-if="offer" class="muted sm">{{ [offer.name, offerKindLabel(offer.kind)].filter((part, index, all) => all.indexOf(part) === index).join(' · ') }}</span></div>
        <p v-if="service.courses.length === 0" class="muted">Esta mesa aún no tiene pases.</p>
        <ol class="cpass">
          <li v-for="(course, index) in service.courses" :key="course.id" :class="{ current: current?.id === course.id }" :data-testid="`course-${course.id}`">
            <span class="pdot" :class="`st-${courseStep(course)}`"><Icon v-if="courseStep(course) === 'done'" name="check" :size="16" :stroke="2.6" /><template v-else>{{ index + 1 }}</template></span>
            <span class="cp-t">
              <span class="cp-head"><b>{{ course.name }}</b><span class="pill" :class="pillTone(course)">{{ courseState(course) }}</span></span>
              <small v-if="courseDetail(course, service.pax)">{{ courseDetail(course, service.pax) }}</small>
              <ul v-if="showPreparations(course)" class="preps">
                <li v-for="preparation in course.preparations" :key="preparation.id" class="prep-line" :data-testid="`prep-${preparation.id}`">
                  <span><b>{{ preparation.name }}</b><template v-if="preparation.quantity > 1"> × {{ preparation.quantity }}</template> · {{ preparationLabel(preparation.state) }}</span>
                  <span class="meta">{{ guestLabel(preparation.guestPosition) }} · {{ names.station(preparation.stationId) }}</span>
                  <span v-for="restriction in preparation.restrictions ?? []" :key="restriction.id" class="line-alert" :class="{ alarm: restrictionAlarming(restriction) }">
                    <Icon name="alert" :size="15" :stroke="2.2" />{{ restrictionLabel(restriction) }}</span>
                  <span v-if="reviewLabel(preparation)" class="line-alert" :class="{ alarm: preparation.reviewPending }">
                    <Icon :name="preparation.reviewPending ? 'alert' : 'checkCircle'" :size="15" :stroke="2.2" />{{ reviewLabel(preparation) }}</span>
                </li>
              </ul>
            </span>
          </li>
        </ol>
      </section>

      <!-- C3b: lo ya enviado de bebida y extras (tambien lo de Caja), con formato, cantidad, comensal, hora y «Anulado en Caja». Sin
           importes ni totales: no es la cuenta provisional (ADR-007, apartado C3b). -->
      <section v-if="entry.consumptions !== undefined" class="card had-card" data-testid="had">
        <div class="card-h"><h3>{{ HAD_TITLE }}</h3><span class="muted sm">{{ HAD_SUB }}</span></div>
        <p v-if="had.length === 0" class="muted sm" data-testid="had-empty">{{ HAD_EMPTY }}</p>
        <ul v-else class="had">
          <li v-for="line in had" :key="line.id" :class="{ voided: line.voided }" :data-testid="`had-${line.id}`">
            <span class="had-q">{{ line.quantity }}×</span>
            <span class="had-n">
              <span class="had-name">{{ line.name }}</span>
              <small v-if="hadDetail(line)">{{ hadDetail(line) }}</small>
              <span v-if="line.voided" class="pill p-void" data-testid="had-voided">{{ VOIDED }}</span>
            </span>
            <span class="had-m">{{ line.at ? clockLabel(line.at) : '' }}</span>
          </li>
        </ul>
        <p class="muted sm had-foot">{{ HAD_FOOT }}</p>
      </section>

      <section class="actions" data-testid="actions">
        <details v-if="hasMore" class="more" :open="more" data-testid="more-options" @toggle="more = ($event.target as HTMLDetailsElement).open">
          <summary><Icon name="dots" :stroke="2.6" /><span>Más opciones</span><Icon :name="more ? 'up' : 'down'" /></summary>
          <div class="more-b">
            <div v-if="reasonInMore" class="field">
              <label for="reason">{{ reasonText }}</label>
              <input id="reason" v-model="reason" name="reason" maxlength="200" autocomplete="off" :disabled="locked" />
            </div>
            <button v-for="action in secondary" :key="action.testId" type="button" class="btn" :class="{ 'btn-caution': action.caution }" :disabled="locked"
              :data-testid="action.testId" @click="act(action)"><Icon :name="ICON[action.action] ?? 'right'" />{{ action.label }}{{ action.needsReason ? ' (con motivo)' : '' }}</button>
            <template v-if="canRemove">
              <button v-for="restriction in restrictions" :key="restriction.id" type="button" class="btn btn-caution" :disabled="locked"
                :data-testid="`do-remove-restriction-${restriction.id}`" @click="remove(restriction)"><Icon name="x" />Retirar · {{ restrictionLabel(restriction) }} (con motivo)</button>
            </template>
          </div>
        </details>
        <div class="bar">
          <RoundTray v-if="canConsume && round.lines.length > 0" v-model:open="trayOpen" embedded :round="round" :locked="locked" :uncertain="uncertain" :message="roundMessage"
            :sent="sent" :bad-line="round.problem?.line ?? null" @send="sendRound" @edit="editing = $event" />
          <p v-if="problem" class="notice" role="alert" data-testid="action-problem"><Icon name="info" />{{ problem }}</p>
          <div v-if="reasonInBar" class="field">
            <label for="reason">{{ reasonText }}</label>
            <input id="reason" v-model="reason" name="reason" maxlength="200" autocomplete="off" :disabled="locked" />
          </div>
          <button v-if="primary" type="button" class="btn btn-primary btn-big" :disabled="locked" :data-testid="primary.testId" @click="act(primary)">
            <Icon :name="ICON[primary.action] ?? 'right'" :size="22" />{{ primary.label }}</button>
          <button v-else type="button" class="btn btn-big waiting" disabled data-testid="do-wait"><Icon name="clock" :size="22" />{{ waitingText(service) }}</button>
        </div>
      </section>
    </template>

    <DishPanel v-else-if="panel === 'carta'" :entry="entry" :offer="offer" :table-name="tableName" :locked="locked" @run="send" />
    <template v-else>
      <DrinksPanel :entry="entry" :products="products" :status="catalogStatus" :can-consume="canConsume" :round="round" @add="addToRound" />
      <RoundTray v-if="canConsume" v-model:open="trayOpen" :round="round" :locked="locked" :uncertain="uncertain" :message="roundMessage" :sent="sent"
        :bad-line="round.problem?.line ?? null" @send="sendRound" @edit="editing = $event" />
    </template>

    <LineSheet v-if="editLine" :key="editing ?? -1" :line="editLine" :pax="service.pax" :restrictions="restrictions" :image-sha="imageOf(editLine.productId)"
      @done="editDone" @remove="editRemove" @close="editing = null" />

    <template v-if="leaving">
      <div class="scrim" aria-hidden="true" @click="leaving = false"></div>
      <section class="sheet" role="alertdialog" aria-modal="true" aria-labelledby="leave-title" data-testid="leave-sheet" @keydown.esc="leaving = false">
        <div class="sheet-scroll">
          <h3 id="leave-title" class="sh-title">¿Salir sin enviar la ronda?</h3>
          <p class="leave-t">{{ leaveText(round.units) }}</p>
          <p class="muted sm">{{ roundNames(round.lines) }}</p>
        </div>
        <div class="sheet-foot">
          <button type="button" class="btn btn-caution" data-testid="leave-discard" @click="discardAndLeave">Salir sin enviar</button>
          <button ref="stay" type="button" class="btn btn-primary" data-testid="leave-stay" @click="leaving = false">Seguir con la ronda</button>
        </div>
      </section>
    </template>
  </article>
</template>
