import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { call, type Session } from './api'
import { CommandRunner, tabLock, type Outcome, type RunnerState } from './commands'
import { LiveBoard, type LiveState } from './live'
import { MoneyLeakError } from './money-guard'
import { indexedDbPendingStore } from './pending'
import { connectHub } from './realtime'
import type { BoardEntry } from './types'

// Fontaneria comun del comandero (D6.2/D6.3) y de la pantalla de cocina (D6.4): la MISMA lectura en vivo
// del tablero y la MISMA orden incierta durable. Una pantalla nueva no reimplementa ninguna de las dos.

// C3e.1 (usabilidad 28-10, H20): tras una orden CONFIRMADA lanzada desde el boton grande (o desde «Servido» de la banda), ese boton
// ensena el resultado, apagado y sin accion («Servido · Aperitivos ✓», «Mesa 5 abierta ✓»), y solo despues la accion siguiente en el
// mismo sitio: un doble toque nervioso, o un segundo toque «por si no fue» con la Wi-Fi lenta, ya no manda el pase siguiente a cocina.
// Quien envia la orden dice el texto y que boton lo pinta (place, un nombre suyo). Sale en cuanto se confirma y dura DONE_MS desde que
// termina la relectura posterior (la accion siguiente aparece DONE_MS despues que antes); mientras tanto todo queda bloqueado (locked),
// como durante la relectura. Rechazada, sin confirmar o sin enviar: nada de «✓», como siempre.
export const DONE_MS = 1500
export interface Done { text: string; place: string }

export function useOperations(props: { token: string; session: Session }, onUnauthorized: () => void) {
  // window.fetch se pasa como funcion suelta: invocarlo con receptor lanza Illegal invocation (D6.3).
  const state = reactive<LiveState>({ entries: [], readAt: null, link: 'connecting', error: '' })
  const runnerState = reactive<RunnerState>({ pending: null, blocked: null, busy: false, lastFailure: null, notice: '' })
  const runner = new CommandRunner(runnerState, {
    fetcher: fetch, store: indexedDbPendingStore, token: () => props.token, lock: tabLock,
    identity: { installationId: props.session.installationId, actor: props.session.actor },
  })
  let lastAutoRetry = 0
  const live = new LiveBoard(state, {
    load: () => call<BoardEntry[]>(fetch, '/dining/board', { token: props.token }),
    connect: () => connectHub(fetch, props.token),
    onUnauthorized,
    // El servidor vuelve a responder y la ultima tentativa fallo por RED: se reintenta la MISMA orden (misma clave,
    // mismos bytes; el servidor no duplica). Solo en primer plano: no hay Background Sync. (D6.7) Solo una orden PROPIA
    // (misma instalacion y mismo actor) llega a `pending`: la de otro nombre de dispositivo queda bloqueada y no se reintenta.
    onRead: () => {
      if (runnerState.pending && !runnerState.busy && runnerState.lastFailure === 'network' && Date.now() - lastAutoRetry > 5000) {
        lastAutoRetry = Date.now(); void resolve(runner.retry())
      }
    },
  })

  // `settling`: desde que se pulsa hasta que termina la RELECTURA posterior. Sin el, al confirmarse una orden los botones
  // volvian a habilitarse con la version anterior y un segundo toque rapido daba un falso version_conflict.
  const settling = ref(0)
  // C3e.1 (H20): el resultado a la vista; su temporizador se limpia al desmontar.
  const done = ref<Done | null>(null)
  let doneTimer: number | undefined
  let mounted = true
  const locked = computed(() => settling.value > 0 || done.value !== null || runnerState.busy || runnerState.pending !== null || runnerState.blocked !== null)
  const now = ref(Date.now())
  let clock: number | undefined
  const age = computed(() => state.readAt ? Math.max(0, Math.round((now.value - state.readAt.getTime()) / 1000)) : null)
  const stale = computed(() => age.value === null || age.value > 45)

  // C3a: devuelve el resultado (o null si no hubo orden o fallo antes de enviarla) para que una pantalla pueda seguir a lo confirmado,
  // p. ej. pasar a la ficha de la mesa recien abierta. Al resolverse, la relectura posterior ya termino. C3b: con null, quien la envio
  // mira si quedo guardada como la orden pendiente (bodega.ts, settlementOf): entonces es incierta, no «no enviada».
  // C3e.1 (H20): result, el resultado que pinta el boton que la lanzo si la orden se confirma (nada si no lo dice).
  async function resolve(work: Promise<Outcome | null>, result?: Done): Promise<Outcome | null> {
    settling.value++
    const noticeBefore = runnerState.notice
    let shown = false
    // El resultado sale ANTES de releer: entre la confirmacion y el final de la relectura el boton ya no vuelve a su accion anterior.
    const show = () => { if (result) { window.clearTimeout(doneTimer); done.value = { ...result }; shown = true } }
    try {
      const outcome = await work
      if (!outcome) return null
      if (outcome.kind === 'unauthorized') { onUnauthorized(); return outcome }
      // Una confirmacion retira el aviso ANTERIOR, no el que acaba de poner esta misma orden ("se aplico en un intento anterior").
      if (outcome.kind === 'confirmed' && runnerState.notice === noticeBefore) runnerState.notice = ''
      if (outcome.kind === 'confirmed') show()
      if (outcome.kind !== 'unconfirmed') await live.refresh()
      return outcome
    } catch (error) {
      // La orden se aplico (se cerro con su eco) pero la respuesta traia datos economicos: no se pinta, se avisa y se relee. C3b: se
      // devuelve como CONFIRMADA (sin respuesta), para que quien la envio no la de por no enviada: una ronda no se vuelve a mandar.
      if (error instanceof MoneyLeakError) { runnerState.notice = `La orden SÍ se aplicó. ${error.message}`; show(); await live.refresh(); return { kind: 'confirmed', response: null } }
      if (!runnerState.notice) runnerState.notice = 'No se pudo enviar la orden.'
      return null
    } finally {
      settling.value--
      // DONE_MS desde que termino la relectura; si la pantalla ya se desmonto, nada que esperar.
      if (shown && mounted) doneTimer = window.setTimeout(() => { done.value = null }, DONE_MS)
      else if (shown) done.value = null
    }
  }
  const run = (path: string, body: Record<string, unknown>, description: string, result?: Done) => resolve(runner.send(path, body, description), result)
  const retry = () => resolve(runner.retry())
  async function reconcile(): Promise<void> { if (await runner.reconcile() === 'applied') await live.refresh() }
  const discard = () => runner.discardBlocked()

  const online = () => { void live.refresh() }
  const visible = () => { if (document.visibilityState === 'visible') { void runner.restore(); void live.refresh() } }   // otra pestana, o el movil vuelve del bloqueo

  onMounted(async () => {
    clock = window.setInterval(() => { now.value = Date.now() }, 1000)
    window.addEventListener('online', online); document.addEventListener('visibilitychange', visible)
    await runner.restore()                          // ANTES de nada: una orden sin confirmar de una sesion anterior manda
    void live.start()
  })
  onUnmounted(() => {
    mounted = false; window.clearTimeout(doneTimer)
    window.clearInterval(clock); window.removeEventListener('online', online); document.removeEventListener('visibilitychange', visible); void live.stop()
  })

  return { state, runnerState, live, locked, done, now, age, stale, run, retry, reconcile, discard }
}
