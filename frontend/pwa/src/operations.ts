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
  const locked = computed(() => settling.value > 0 || runnerState.busy || runnerState.pending !== null || runnerState.blocked !== null)
  const now = ref(Date.now())
  let clock: number | undefined
  const age = computed(() => state.readAt ? Math.max(0, Math.round((now.value - state.readAt.getTime()) / 1000)) : null)
  const stale = computed(() => age.value === null || age.value > 45)

  // C3a: devuelve el resultado (o null si no hubo orden o fallo antes de enviarla) para que una pantalla pueda seguir a lo confirmado,
  // p. ej. pasar a la ficha de la mesa recien abierta. Al resolverse, la relectura posterior ya termino.
  async function resolve(work: Promise<Outcome | null>): Promise<Outcome | null> {
    settling.value++
    const noticeBefore = runnerState.notice
    try {
      const outcome = await work
      if (!outcome) return null
      if (outcome.kind === 'unauthorized') { onUnauthorized(); return outcome }
      // Una confirmacion retira el aviso ANTERIOR, no el que acaba de poner esta misma orden ("se aplico en un intento anterior").
      if (outcome.kind === 'confirmed' && runnerState.notice === noticeBefore) runnerState.notice = ''
      if (outcome.kind !== 'unconfirmed') await live.refresh()
      return outcome
    } catch (error) {
      // La orden se aplico (se cerro con su eco) pero la respuesta traia datos economicos: no se pinta, se avisa y se relee.
      if (error instanceof MoneyLeakError) { runnerState.notice = `La orden SÍ se aplicó. ${error.message}`; await live.refresh() }
      else if (!runnerState.notice) runnerState.notice = 'No se pudo enviar la orden.'
      return null
    } finally { settling.value-- }
  }
  const run = (path: string, body: Record<string, unknown>, description: string) => resolve(runner.send(path, body, description))
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
  onUnmounted(() => { window.clearInterval(clock); window.removeEventListener('online', online); document.removeEventListener('visibilitychange', visible); void live.stop() })

  return { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard }
}
