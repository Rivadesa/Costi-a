<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { addProblem, HAD_EMPTY, HAD_FOOT, HAD_SUB, HAD_TITLE, hadDetail, leaveText, ROUND_SENT, ROUND_UNCONFIRMED, roundNames, roundProblem, VOIDED } from '../bodega'
import type { CatalogStatus } from '../catalog'
import { offerFor, type Configuration, type Names } from '../configuration'
import type { DrinkProduct, RoundItem } from '../drinks'
import { barNote, commandFor, dishesHint, doneLabel, PENDING_REVIEW_TEXT, primaryAction, REASON_PROBLEM, reasonLabel, reasonOk, serviceCommand, tableActions, tableInfo, waitingText, type TableAction } from '../ficha'
import { isTileCode, restrictionAlarming, tileLabel, tileState } from '../floor'
import type { IconName } from '../icons'
import { clockLabel, plural, restrictionLabel } from '../labels'
import { Round, type Settlement } from '../round'
import type { BoardEntry, GuestRestriction } from '../types'
import AllergySheet from './AllergySheet.vue'
import CoursesTimeline from './CoursesTimeline.vue'
import DishPanel from './DishPanel.vue'
import DrinksPanel from './DrinksPanel.vue'
import Icon from './Icon.vue'
import LineSheet from './LineSheet.vue'
import RoundTray from './RoundTray.vue'
import SkipSheet from './SkipSheet.vue'

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
// C3e (plan 2026-10-27, paso 3; maqueta 2026-10-22, «2 · Sala · Mesa» y «6 · Sala · servido»): los pases salen a CoursesTimeline (los
// platos agrupados con la regla comun de pases.ts y cocina.ts, sus excepciones aparte y «Siguiente»); encima del boton grande, su nota
// (barNote); omitir un pase abre su propia hoja (SkipSheet) con su motivo; y las alergias se ven, se anotan y se retiran en su hoja
// (AllergySheet), desde la cabecera de la franja roja y solo con lo que el servidor anuncia. «Más opciones» se queda con lo demas.
// C3e.1 (usabilidad 28-10, H20): el boton grande manda tambien el texto de su resultado (ficha.ts, doneLabel); confirmada su orden,
// BoardView lo devuelve en done y el boton lo ensena apagado durante DONE_MS (operations.ts) antes de la accion siguiente.
// C3e.1 (usabilidad 28-10, H03): las hojas de alergias y de omitir ya no se cierran al tocar: mandan su orden por sendSettled (la de la
// ronda, ahora con un nombre que no es solo de la ronda) y se cierran solo si se confirma o queda sin confirmar (commands.ts, notApplied).
const props = defineProps<{
  entry: BoardEntry; tableName: string; roomName: string; configuration: Configuration; names: Names
  products: DrinkProduct[]; catalogStatus: CatalogStatus; canConsume: boolean; locked: boolean; uncertain: boolean; now: number; done: string
  sendSettled: (path: string, body: Record<string, unknown>, description: string) => Promise<Settlement>
}>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string, result?: string]; back: []; bodega: [] }>()

const service = computed(() => props.entry.service)
const has = (action: string) => service.value.actions?.includes(action) === true
const panel = ref<'ficha' | 'carta' | 'bodega'>('ficha')
watch(() => service.value.id, () => { panel.value = 'ficha'; problem.value = ''; skipping.value = null; allergiesOpen.value = false })
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
const people = computed(() => plural(service.value.pax, 'persona', 'personas'))
// En Bebidas, como en la maqueta: «Mesa 4 · 4 personas» bajo el titulo, sin el estado de la mesa.
const bodegaSub = computed(() => `${props.tableName} · ${people.value}`)
// C3e (revision): en la ficha, una sola linea como la maqueta («Sala 1 · Menú del día · 4 personas»); las horas ya las dicen la nota del
// boton grande (barNote) y la de cada pase. En Carta, cada toque es una orden: el NOMBRE de la mesa va escrito bajo el titulo («Mesa 3 ·
// Sala · Carta»), como en Bebidas, porque en el movil «Volver» es solo la flecha y la banda puede nombrar OTRA mesa.
const fichaSub = computed(() => [props.roomName, offer.value?.name ?? '', people.value].filter(part => part).join(' · '))
const cartaSub = computed(() => [props.tableName, roomAndOffer.value].filter(part => part).join(' · '))

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
  const settlement = await props.sendSettled(serviceCommand(service.value, 'add-consumptions'), request.body, request.description)
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
// C3c: «Ver mesa» de la banda de otra mesa sale por aqui con `then` (ir a esa mesa) en lugar de volver al plano: tambien pregunta.
let afterLeave: (() => void) | null = null
function leave(then?: () => void): void {
  if (round.lines.length > 0 && !round.sending) { afterLeave = then ?? null; leaving.value = true; return }
  if (then) then(); else emit('back')
}
const stay = ref<HTMLButtonElement | null>(null)
watch(leaving, open => { if (open) void nextTick(() => stay.value?.focus()); else afterLeave = null })
function discardAndLeave(): void {
  const then = afterLeave
  leaving.value = false; round.clear()
  if (then) then(); else emit('back')
}
// C3c: «Ver mesa» de la banda de ESTA mesa: la ficha (desde Carta o Bodega; la ronda sigue, es de la ficha).
function show(): void { panel.value = 'ficha'; window.scrollTo({ top: 0 }) }
// C3e (revision): BoardView lee en que panel esta la ficha para no repetir en la banda el «Servido» de ESTA mesa mientras se ve su boton
// grande (maqueta «6 · Sala · servido», «Al tocar «Ver mesa»»). Solo se lee: el panel lo cambia la ficha.
defineExpose({ leave, show, panel })

// ---- Lo que lleva la mesa: bebidas y extras ya enviados (tambien los de Caja), sin importes ni totales ----
const had = computed(() => props.entry.consumptions ?? [])

// ---- Restricciones: siempre escritas; el rojo solo cuando alarman (alergia, grave, desconocida o sin revisar por cocina). ----
const restrictions = computed<GuestRestriction[]>(() => service.value.restrictions ?? [])
const alarming = computed(() => service.value.restrictionsPendingAck === true || restrictions.value.some(restrictionAlarming))
const restrictionTone = computed(() => alarming.value ? '' : restrictions.value.length > 0 ? 'warn' : 'none')
// C3e: la cabecera de la franja abre la hoja de alergias SOLO con lo que anuncia el servidor (D3.1): «Cambiar» si deja anotar o retirar,
// «Anotar alergia o intolerancia» si aun no hay ninguna y deja anotar. Sin ninguna de las dos (una mesa terminada), «Ver alergias» abre
// la hoja solo para leer, o nada si no hay ninguna. Abrir la hoja no envia nada: no es un boton do- y no se apaga con una orden incierta.
const allergiesOpen = ref(false)
const allergyOpener = computed(() => {
  const declare = has('declare-restriction'), remove = has('remove-restriction')
  if (restrictions.value.length === 0) return declare ? 'Anotar alergia o intolerancia' : ''
  return declare || remove ? 'Cambiar' : 'Ver alergias'
})

// ---- Acciones ----
const problem = ref('')
const reason = ref('')
const actions = computed(() => tableActions(props.entry))
// E4e: con el pase que va a salir esperando la revision de cocina, «Iniciar servicio» no va en grande (sigue en «Más opciones»).
const primary = computed(() => primaryAction(actions.value, service.value))
const secondary = computed(() => actions.value.filter(action => action !== primary.value))
// C3e: la linea encima del boton grande («Cada estación lo recibe…», «Listo en el pase desde las 21:19.»), de la lectura (ficha.ts).
const note = computed(() => barNote(service.value, primary.value))
// C3e: omitir lleva su motivo en SU hoja; el campo de «Más opciones» queda para lo demas que lo pide (pausar, cancelar, liberar).
const sharesReason = (action: TableAction) => action.needsReason && action.action !== 'skip'
// Un solo campo de motivo: junto al boton grande si este lo pide; si no, en «Más opciones».
const reasonInBar = computed(() => primary.value !== null && sharesReason(primary.value))
const reasonInMore = computed(() => !reasonInBar.value && secondary.value.some(sharesReason))
const reasonText = computed(() => reasonLabel(actions.value.filter(action => action.action !== 'skip').map(action => action.action)))
const hasMore = computed(() => secondary.value.length > 0)
const more = ref(false)
// La hoja de omitir es de UN pase y vive mientras el servidor siga anunciando omitirlo: si otro puesto lo envia, la hoja se cierra.
const skipping = ref<string | null>(null)
const skipAction = computed(() => skipping.value === null ? null : actions.value.find(action => action.action === 'skip' && action.testId === skipping.value) ?? null)
watch(skipAction, action => { if (action === null) skipping.value = null })
// Sin nada que ver ni anunciado (se retiro la ultima y ya no se deja anotar), la hoja de alergias se cierra.
watch(allergyOpener, opener => { if (!opener) allergiesOpen.value = false })
const ICON: Record<string, IconName> = {
  serve: 'check', ready: 'checkCircle', 'fire-next': 'send', start: 'play', resume: 'play', complete: 'flag', release: 'door',
  pause: 'pause', skip: 'skip', 'cancel-unstarted': 'x',
}

function send(path: string, body: Record<string, unknown>, description: string, result?: string): void { problem.value = ''; emit('run', path, body, description, result) }
function act(action: TableAction, result?: string): void {
  problem.value = ''
  if (action.needsReason && !reasonOk(reason.value)) { problem.value = REASON_PROBLEM; if (!reasonInBar.value) more.value = true; return }
  const request = commandFor(props.entry, action, props.tableName, reason.value)
  send(request.path, request.body, request.description, result)
  if (action.needsReason) reason.value = ''
}
// El boton grande: su resultado se escribe con ESTA lectura (tras enviar un pase, fire-next ya nombraria el siguiente).
function actBig(): void { if (primary.value) act(primary.value, doneLabel(primary.value, service.value)) }
// En «Más opciones», omitir abre su hoja; lo demas sale como siempre.
function pick(action: TableAction): void {
  problem.value = ''
  if (action.action === 'skip') skipping.value = action.testId
  else act(action)
}
// C3e.1 (usabilidad 28-10, H03): las ordenes de las hojas (anotar o retirar una alergia, omitir un pase) esperan su resultado, que vuelve
// a la hoja: ella decide si se cierra o se queda con lo escrito. Sin confirmar, la pantalla sube hasta el aviso de la orden pendiente,
// como con la ronda (la hoja se cierra y ese aviso es el que la resuelve).
async function settle(path: string, body: Record<string, unknown>, description: string): Promise<Settlement> {
  problem.value = ''
  const settled = await props.sendSettled(path, body, description)
  if (settled.kind === 'unconfirmed') window.scrollTo({ top: 0 })
  return settled
}
// «Omitir Postre» de la hoja: UNA orden skip con la version leida (F01), solo si el servidor aun lo anuncia (la hoja ya comprobo el motivo).
// Confirmada, el servidor deja de anunciar omitir ese pase y la hoja se cierra sola (skipAction); si no, la hoja dice que paso.
function skip(skipReason: string): Promise<Settlement> {
  const action = skipAction.value
  if (!action) return Promise.resolve({ kind: 'not_sent' })
  const request = commandFor(props.entry, action, props.tableName, skipReason)
  return settle(request.path, request.body, request.description)
}
</script>

<template>
  <article class="ficha stack" :class="{ 'ficha-main': panel === 'ficha' }" data-testid="detail">
    <header class="appbar">
      <button v-if="panel === 'ficha'" type="button" class="back" data-testid="back" @click="leave()"><Icon name="back" :size="24" :stroke="2.2" /><span class="back-t">Mesas</span></button>
      <button v-else type="button" class="back" data-testid="back" @click="panel = 'ficha'"><Icon name="back" :size="24" :stroke="2.2" /><span class="back-t">{{ tableName }}</span></button>
      <div class="appbar-t">
        <div class="appbar-title">
          <h2>{{ panel === 'ficha' ? tableName : PANEL_TITLES[panel] }}</h2>
          <span v-if="panel !== 'bodega'" class="pill" :class="tone" data-testid="detail-state">{{ tileLabel(state) }}</span>
        </div>
        <p v-if="panel === 'bodega'" class="appbar-sub" data-testid="detail-info">{{ bodegaSub }}</p>
        <p v-else-if="panel === 'ficha'" class="appbar-sub" data-testid="detail-info">{{ fichaSub }}</p>
        <template v-else>
          <p class="appbar-sub" data-testid="carta-table">{{ cartaSub }}</p>
          <p class="appbar-sub" data-testid="detail-info">{{ info.head }}<span v-if="info.tail" class="nw">{{ info.tail }}</span></p>
        </template>
      </div>
    </header>

    <!-- Las restricciones, arriba y escritas: quien, que y gravedad. Tambien dentro de Carta y Bodega (alli sin abrir la hoja). C3e: la
         cabecera abre la hoja de alergias (verlas con sus platos, anotar y retirar), solo con lo que anuncia el servidor. -->
    <section v-if="panel === 'ficha' || restrictions.length > 0 || service.restrictionsPendingAck" class="alert-box" :class="restrictionTone"
      aria-label="Restricciones de los comensales" data-testid="restrictions">
      <div class="alert-h fc-al-h">
        <Icon :name="restrictions.length > 0 || service.restrictionsPendingAck ? 'alert' : 'checkCircle'" :size="18" :stroke="2.2" />
        <span class="fc-al-ht">{{ restrictions.length > 0 ? 'Restricciones de los comensales' : 'Sin restricciones declaradas' }}</span>
        <button v-if="panel === 'ficha' && allergyOpener" type="button" class="fc-al-open" aria-haspopup="dialog" data-testid="restrictions-open"
          @click="allergiesOpen = true">{{ allergyOpener }}</button>
      </div>
      <ul v-if="restrictions.length > 0" class="achips">
        <li v-for="restriction in restrictions" :key="restriction.id" class="achip" :class="{ strong: restrictionAlarming(restriction) }">{{ restrictionLabel(restriction) }}</li>
      </ul>
      <p v-if="service.restrictionsPendingAck" class="alert-f" data-testid="restrictions-pending">{{ PENDING_REVIEW_TEXT }}</p>
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

      <CoursesTimeline :service="service" :names="names" :offer="offer" />

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
            <!-- Omitir abre su hoja (sin «(con motivo)»: el motivo se pide alli); lo demas que pide motivo usa el campo de arriba. -->
            <button v-for="action in secondary" :key="action.testId" type="button" class="btn" :class="{ 'btn-caution': action.caution }" :disabled="locked"
              :aria-haspopup="action.action === 'skip' ? 'dialog' : undefined" :data-testid="action.testId" @click="pick(action)">
              <Icon :name="ICON[action.action] ?? 'right'" />{{ action.label }}{{ sharesReason(action) ? ' (con motivo)' : '' }}</button>
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
          <!-- C3e: la nota del boton grande (maqueta: «Cada estación lo recibe en su tableta al momento.», «Listo en el pase desde las 21:19.»). -->
          <p v-if="note && !done" class="bar-note" data-testid="bar-note">{{ note }}</p>
          <!-- C3e.1 (H20): confirmada la orden del boton grande, su resultado apagado y sin accion; despues, la accion siguiente. -->
          <button v-if="done" type="button" class="btn btn-big btn-done" disabled data-testid="do-done"><Icon name="check" :size="22" :stroke="2.6" />{{ done }}</button>
          <button v-else-if="primary" type="button" class="btn btn-primary btn-big" :disabled="locked" :data-testid="primary.testId" @click="actBig">
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
    <!-- C3e: las hojas de la ficha. Cada una es de ESTA lectura y de lo que anuncia ahora el servidor. -->
    <SkipSheet v-if="panel === 'ficha' && skipAction?.course" :key="skipAction.testId" :course-name="skipAction.course.name" :locked="locked"
      :send="skip" @close="skipping = null" />
    <AllergySheet v-if="panel === 'ficha' && allergiesOpen" :entry="entry" :table-name="tableName" :locked="locked" :send="settle" @close="allergiesOpen = false" />

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
