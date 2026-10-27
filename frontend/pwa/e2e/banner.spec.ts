import { expect, test, type APIRequestContext, type Locator } from '@playwright/test'
import {
  checkDevice, checkScreen, command, deviceName, finishAndRelease, kitchenValidates, lane, MAIN, openTable, pairByApi, pairThroughTheUi, post, press, read,
  recordVibrations, vibrations, watch,
} from './support'

// C3c (plan 2026-10-22, seccion 4.1): el aviso de «listo para servir» de Costina Sala contra el MOTOR REAL, en los dos proyectos. La
// cocina valida un pase (por API, con el token de la tablet del pase) y el camarero, con «Bodega y bebidas» abierto en esa mesa, ve la
// banda debajo de la cabecera sin que tape volver, el buscador ni la bandeja de la ronda; «Servido» desde la banda es UNA orden y la
// version sube en uno. Una alergia declarada despues de validar quita la banda hasta que el pase la revisa (el servidor deja de
// anunciar serve) y entonces vuelve a avisar. «Ver mesa» abre la ficha, la mesa se pinta en verde y «Visto» quita la banda solo en
// este movil y solo en memoria. La vibracion se cuenta con un navigator.vibrate de mentira: Chromium de escritorio no vibra.
// Usa la mesa del TABLERO de su carril (e2e/support.ts) y la deja libre al terminar: board.spec.ts, que corre despues por orden
// alfabetico, la abre otra vez.
type Course = { id: string; name: string; state: string; readyAt: string | null; preparations: Array<{ id: string; state: string; reviewPending?: boolean }> }
const courses = async (request: APIRequestContext, serviceId: string) => (await read(request, serviceId)).data.courses as unknown as Course[]

// La banda OCUPA su sitio: lo de debajo empieza donde ella acaba, y la bandeja (fija abajo) no la pisa.
async function apart(upper: Locator, lower: Locator, what: string): Promise<void> {
  const [a, b] = [await upper.boundingBox(), await lower.boundingBox()]
  expect(a && b ? Math.round(a.y + a.height) <= Math.round(b.y) : false, `the banner covers ${what}`).toBeTruthy()
}

test('Sala: «listo para servir» shows over Bodega without covering it, vibrates once, «Servido» from the banner is ONE command; «Ver mesa», green tile and «Visto»', async ({ page, request }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const { board: table } = lane()
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  await recordVibrations(page)
  const serves: string[] = []
  page.on('request', sent => { if (sent.method() === 'POST' && sent.url().includes('/commands/serve')) serves.push(sent.headers()['idempotency-key'] ?? '') })

  const serviceId = await openTable(request, table.id, 2)
  let passed = false
  try {
    await command(request, serviceId, 'start'); await command(request, serviceId, 'fire-next')
    const pass = await pairByApi(request, deviceName('banner-pase'), 'kitchen', 'pase')
    const [first, second] = await courses(request, serviceId)

    // 1) El camarero, en «Bodega y bebidas» de esa mesa (la ficha sustituye al plano: no se ve la mesa).
    await pairThroughTheUi(page, request, deviceName('banner'), 'service', 'sala-1')
    await checkDevice(page)
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    const tile = page.getByTestId(`table-${table.id}`)
    await expect(tile.getByTestId('tile-state')).toHaveText('En cocina', { timeout: 15_000 })
    await expect(page.getByTestId('ready-banner')).toHaveCount(0)
    await press(tile)
    await press(page.getByTestId('entry-bodega'))
    await expect(page.getByTestId('panel-bodega')).toBeVisible()

    // 2) Cocina valida: la banda sale sola, con el NOMBRE de la mesa y del pase, debajo de la cabecera y sin tapar nada.
    await kitchenValidates(request, serviceId, first.id, pass)
    const banner = page.getByTestId('ready-banner')
    await expect(banner).toBeVisible({ timeout: 15_000 })
    await expect(banner.getByTestId('banner-title')).toHaveText(`${table.name} · ${first.name}`)
    await expect(banner).toContainText('listo para servir')
    await expect(banner).toContainText('El pase lo validó a las')
    await expect(banner).not.toContainText(new RegExp(`${table.id}\\b|${serviceId}`))
    await expect(page.getByTestId('banner-serve')).toHaveText(`Servido · ${first.name}`)
    await expect(page.getByTestId('banner-more')).toHaveCount(0)
    for (const [target, what] of [[page.getByTestId('back'), 'the back button'], [page.getByTestId('drinks-search'), 'the Bodega search']] as const) {
      await expect(target).toBeInViewport()
      await apart(banner, target, what)
    }
    await expect(page.getByTestId('round')).toBeInViewport()
    await apart(banner, page.getByTestId('round'), 'the round tray')
    await checkScreen(page, 'banner over Bodega')
    await expect.poll(() => vibrations(page)).toBe(1)                                    // una clave nueva, con la pagina a la vista y ya tocada

    // 3) Alergia declarada DESPUES de validar: el pase sigue listo, pero el servidor deja de anunciar servir hasta que cocina revise.
    //    E4e (ADR-016): tambien queda por revisar el plato del comensal en el pase sin enviar; el pase lo revisa antes de que salga.
    await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'sésamo', severity: 'Severe' })
    await expect(banner).toHaveCount(0, { timeout: 15_000 })
    expect((await courses(request, serviceId))[0].state).toBe('Ready')
    expect((await courses(request, serviceId))[1].preparations.some(p => p.reviewPending)).toBeTruthy()
    // Primero el pase sin enviar y al final el listo: la banda vuelve con la ULTIMA revision, asi que «Servido» lleva la version al dia (F01).
    for (const course of (await courses(request, serviceId)).reverse()) for (const preparation of course.preparations.filter(p => p.reviewPending)) {
      const reviewed = await post(request, pass, `/dining/services/${serviceId}/commands/review-preparation`,
        { expectedVersion: (await read(request, serviceId)).version, courseId: course.id, itemId: preparation.id, decision: 'unaffected', note: 'no lleva sésamo' })
      expect(reviewed.ok(), `review -> ${reviewed.status()}`).toBeTruthy()
    }
    await expect(banner).toBeVisible({ timeout: 15_000 })                                // revisado: vuelve, y vuelve a vibrar
    await expect.poll(() => vibrations(page)).toBe(2)

    // 4) «Servido» desde la banda: UNA orden, con la version leida, y la version del servicio sube exactamente en uno.
    const before = (await read(request, serviceId)).version
    await press(page.getByTestId('banner-serve'))
    await expect(banner).toHaveCount(0, { timeout: 15_000 })
    await expect.poll(async () => (await courses(request, serviceId))[0].state).toBe('Served')
    expect((await read(request, serviceId)).version).toBe(before + 1)
    expect(serves.length).toBe(1)
    await expect(page.getByTestId('pending')).toHaveCount(0)
    await expect(page.getByTestId('panel-bodega')).toBeVisible()                         // el camarero sigue donde estaba

    // 5) Siguiente pase listo: «Ver mesa» lleva a la ficha de esa mesa; en el plano, la mesa en verde; «Visto» quita la banda.
    await command(request, serviceId, 'fire-next')
    await kitchenValidates(request, serviceId, second.id, pass)
    await expect(banner).toBeVisible({ timeout: 15_000 })
    await expect(banner.getByTestId('banner-title')).toHaveText(`${table.name} · ${second.name}`)
    await expect.poll(() => vibrations(page)).toBe(3)
    await press(page.getByTestId('banner-view'))
    await expect(page.getByTestId('entry-bodega')).toBeVisible()
    await expect(page.getByTestId('detail-state')).toHaveText('Listo para servir')
    await press(page.getByTestId('back'))
    await expect(tile).toHaveClass(/\bready\b/)
    await expect(tile.getByTestId('tile-state')).toHaveText('Listo para servir')
    await checkScreen(page, 'banner over the floor, green tile')
    await press(page.getByTestId('banner-seen'))
    await expect(banner).toHaveCount(0)
    await expect(tile).toHaveClass(/\bready\b/)                                           // «Visto» no sirve nada: la mesa sigue en verde
    const reread = page.waitForResponse(response => new URL(response.url()).pathname.endsWith('/dining/board'))
    await press(page.getByTestId('refresh'))
    await reread
    await expect(tile.getByTestId('tile-state')).toHaveText('Listo para servir')
    await expect(banner).toHaveCount(0)                                                   // recordado entre lecturas
    // «Visto» vive en memoria: tras recargar vuelve a salir, sin vibrar (la primera lectura no es nueva).
    await page.reload()
    await expect(page.getByTestId('ready-banner')).toBeVisible({ timeout: 15_000 })
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    expect(await vibrations(page)).toBe(0)
    expect(serves.length).toBe(1)
    expect((await courses(request, serviceId))[1].state).toBe('Ready')

    expect(leaks, 'the PWA received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
    passed = true
  } finally {
    // La mesa queda libre para board.spec.ts. Si la prueba ya fallo, un fallo aqui no tapa el de verdad.
    try { await finishAndRelease(request, serviceId) } catch (error) { if (passed) throw error }
  }
})
