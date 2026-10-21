import { expect, test, type APIRequestContext } from '@playwright/test'
import {
  asMain, bottlePng, checkDevice, checkScreen, command, deviceContext, deviceName, ensureDrink, loseResponses, MAIN, openTable, pairThroughTheUi, press,
  release, storedOrder, watch, type Drink,
} from './support'

// C3b: «Bodega y bebidas» de punta a punta contra el motor real, en escritorio y en movil (plan 2026-10-15, seccion 4.4): buscar «albar»
// -> Copa -> «Enviar»; UNA orden por ronda y un solo efecto tras una respuesta perdida de verdad (se cuentan las LINEAS de la mesa: la
// version del servicio no sube); la ronda en «Lo que lleva la mesa» del otro comandero, en vivo; la mesa que cambia (version_conflict)
// y «enviar de nuevo» con otra clave; salir con una ronda sin enviar pide confirmacion; la foto, pintada sin violaciones de la CSP; y
// nunca un importe ni una palabra de caja.
// La mesa es M1, libre tras pwa_http.py (test_01..04): los dos proyectos la usan uno detras de otro y cada uno la deja libre al terminar
// (test_06 y test_07 de pwa_http.py la necesitan despues).
const TABLE = { id: 'M1', name: 'Mesa 1' }
// El laboratorio no trae ningun vino con «albar»: el PC principal lo crea por API, con su copa, su botella y su foto (una de verdad).
const ALBARINO: Drink = {
  id: 'e2e-albarino', name: 'Albariño Pazo de Señoráns 2023', categoryId: 'vinos', reference: 'E2E-ALB',
  presentations: [{ id: 'botella-0-75-l', name: 'Botella 0.75 L', cents: 3200 }], glassCents: 600, image: bottlePng([200, 170, 70]),
}
type Charge = { id: string; productId?: string | null; presentationId?: string | null; quantity: number; guestPosition?: number | null; origin?: string | null; voided: boolean }
// Lo que hay DE VERDAD en la mesa (la cuenta, leida como el PC principal; el comandero nunca la ve): las lineas de bebida y extras.
async function drinkLines(request: APIRequestContext, serviceId: string): Promise<Array<[string, string, number, number | null]>> {
  const account = await (await asMain(request, `/checkout/services/${serviceId}`)).json() as { data: { charges: Charge[] } }
  return account.data.charges.filter(charge => charge.origin === 'consumption' && !charge.voided)
    .map(charge => [charge.productId ?? '', charge.presentationId ?? '', charge.quantity, charge.guestPosition ?? null])
}

test('Bodega y bebidas: a round is ONE command, applied once after a lost answer, seen live on the other handheld; photos without CSP violations', async ({ page, request, browser }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  await ensureDrink(request, ALBARINO)
  const serviceId = await openTable(request, TABLE.id, 4)
  const other = await deviceContext(browser)
  try {
    // El otro comandero, con la ficha de la misma mesa abierta: ve «Lo que lleva la mesa» vacio.
    const second = await other.newPage()
    await watch(second, leaks, violations)
    await pairThroughTheUi(second, request, deviceName('drinks-b'), 'service', 'sala-1')
    await expect(second.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    await press(second.getByTestId(`table-${TABLE.id}`))
    await expect(second.getByTestId('had-empty')).toBeVisible({ timeout: 15_000 })
    await expect(second.getByTestId('had')).toContainText('Los importes solo se ven en Caja.')

    await pairThroughTheUi(page, request, deviceName('drinks'), 'service', 'sala-1')
    await expect(page.getByTestId('board')).toBeVisible()
    await checkDevice(page)
    const tile = page.getByTestId(`table-${TABLE.id}`)
    await expect(tile.getByTestId('tile-state')).toHaveText('Sentados', { timeout: 15_000 })
    await press(tile)
    await press(page.getByTestId('entry-bodega'))
    await expect(page.getByTestId('panel-bodega')).toBeVisible()
    await expect(page.getByTestId('chip-glass')).toHaveAttribute('aria-pressed', 'true', { timeout: 15_000 })   // la entrada: «Por copa»
    await expect(page.getByTestId('round-empty')).toHaveText('Ronda vacía. Copa o Botella suman 1 para toda la mesa.')
    await checkScreen(page, 'Bodega y bebidas')

    // 1) Buscar «albar», sin tilde: la fila del vino, Copa primero, y su foto pintada con una URL blob: (bytes pedidos con el token).
    await page.getByTestId('drinks-search').fill('albar')
    const row = page.getByTestId(`drink-${ALBARINO.id}`)
    await expect(row).toBeVisible()
    await expect(page.getByTestId('results-count')).toHaveText('1 resultado en toda la bodega y bebidas')
    await expect(row).toContainText('Bebidas › Vinos')
    await expect(row.locator('.pbtns button')).toHaveText(['Copa', /^Botella\s*0\.75 L$/])
    const photo = row.getByTestId('photo')
    await expect(photo).toHaveAttribute('src', /^blob:/, { timeout: 15_000 })
    await expect.poll(() => photo.evaluate(image => (image as HTMLImageElement).complete ? (image as HTMLImageElement).naturalWidth : 0), { timeout: 15_000 }).toBe(96)
    await checkScreen(page, 'search albar')

    // 2) Copa, Copa (una linea con 2) y un agua. La ronda es un borrador en memoria: nada ha llegado al servidor.
    await press(page.getByTestId(`add-${ALBARINO.id}-copa`))
    await press(page.getByTestId(`add-${ALBARINO.id}-copa`))
    await expect(page.getByTestId(`in-round-${ALBARINO.id}`)).toHaveText('2 en la ronda')
    await page.getByTestId('drinks-search').fill('agua')
    await press(page.getByTestId('add-water-bottle'))
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 3 sin enviar')
    expect(await drinkLines(request, serviceId)).toEqual([])
    // El agua, para el comensal 2: desde «Para: Toda la mesa» de la ronda, con su hoja.
    await press(page.getByTestId('round-toggle'))
    await press(page.getByTestId('round-for-1'))
    await expect(page.getByTestId('line-sheet')).toContainText('Agua mineral · Botella')
    await checkScreen(page, 'round line sheet')
    await press(page.getByTestId('sheet-guest-2'))
    await press(page.getByTestId('sheet-done'))
    await expect(page.getByTestId('round-for-1')).toContainText('Para: Comensal 2')
    await checkScreen(page, 'round open')

    // 3) «Enviar» con la RESPUESTA PERDIDA de verdad: el servidor aplica la ronda y el comandero no se entera. Es UNA orden incierta,
    // durable, que se reintenta identica (misma clave). La ronda se vacia: ya ES esa orden y no se manda dos veces.
    const keys: string[] = []
    page.on('request', sent => { if (sent.method() === 'POST' && sent.url().includes('/commands/add-consumptions')) keys.push(sent.headers()['idempotency-key'] ?? '') })
    const lost = await loseResponses(page, '**/commands/add-consumptions')
    await press(page.getByTestId('do-send-round'))
    await lost.lost
    const pending = page.getByTestId('pending')
    await expect(pending).toContainText('ORDEN SIN CONFIRMAR', { timeout: 15_000 })
    await expect(pending).toContainText(`Ronda para ${TABLE.name}: 2 × ${ALBARINO.name} · Copa, 1 × Agua mineral · Botella (Comensal 2)`)
    await expect(page.getByTestId('round-empty')).toBeVisible()
    expect(await drinkLines(request, serviceId)).toEqual([[ALBARINO.id, 'copa', 2, null], ['water', 'bottle', 1, 2]])   // el servidor SI la aplico
    // Mientras tanto se puede llenar otra ronda, pero no enviarla (una sola orden incierta, D6.7).
    await press(page.getByTestId('add-water-bottle'))
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
    await expect(page.getByTestId('do-send-round')).toBeDisabled()
    await checkScreen(page, 'uncertain round')
    await page.unrouteAll({ behavior: 'wait' })
    await page.reload()                                                                    // la orden sigue en el dispositivo y sale sola
    await expect(page.getByTestId('board')).toBeVisible({ timeout: 30_000 })
    await expect.poll(() => storedOrder(page), { timeout: 30_000 }).toBeNull()
    await expect(page.getByTestId('pending')).toHaveCount(0)
    expect(new Set(keys).size, 'one key for the round, however many attempts').toBe(1)
    expect(await drinkLines(request, serviceId)).toEqual([[ALBARINO.id, 'copa', 2, null], ['water', 'bottle', 1, 2]])   // UN solo efecto

    // 4) El OTRO comandero lo ve en «Lo que lleva la mesa» sin tocar nada (aviso service.consumptions_changed y relectura).
    const theirs = second.getByTestId('had')
    await expect(theirs.locator('li')).toHaveCount(2, { timeout: 15_000 })
    await expect(theirs.locator('li').nth(0)).toContainText(`2×${ALBARINO.name}Copa · Toda la mesa`)
    await expect(theirs.locator('li').nth(1)).toContainText('1×Agua mineralBotella · Comensal 2')
    await checkScreen(second, 'what the table has, on the other handheld')
    // Y en el propio: la ficha de la mesa.
    await press(tile)
    const mine = page.getByTestId('had')
    await expect(mine.locator('li')).toHaveCount(2)
    await expect(mine).toContainText('Los importes solo se ven en Caja.')

    // 5) La mesa cambia mientras se envia (otro puesto la pone en servicio): version_conflict. La ronda sigue en la bandeja y se ofrece
    // «La mesa ha cambiado: enviar de nuevo», que es OTRA orden con su propia clave y la version releida.
    await press(page.getByTestId('entry-bodega'))
    await expect(page.getByTestId('round-empty')).toBeVisible()                                // el borrador de antes se perdio al recargar
    await expect(page.getByTestId('at-this-table')).toBeVisible()                              // «En esta mesa»: repetir con +1
    await press(page.getByTestId(`repeat-${ALBARINO.id}-copa`))
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
    await page.route('**/commands/add-consumptions', async route => { await command(request, serviceId, 'start'); await route.continue() }, { times: 1 })
    const send = page.getByTestId('do-send-round')
    await press(send)
    await expect(send).toHaveText(/La mesa ha cambiado: enviar de nuevo/, { timeout: 15_000 })
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
    await expect(page.getByTestId('round-problem')).toContainText('La mesa ha cambiado')
    expect(await drinkLines(request, serviceId)).toHaveLength(2)                               // nada aplicado
    await checkScreen(page, 'version conflict')
    await press(send)
    await expect(page.getByTestId('round-empty')).toHaveText('Ronda enviada: ya sale en «Lo que lleva la mesa».', { timeout: 15_000 })
    expect(await drinkLines(request, serviceId)).toEqual([[ALBARINO.id, 'copa', 2, null], ['water', 'bottle', 1, 2], [ALBARINO.id, 'copa', 1, null]])
    expect(new Set(keys).size, 'the second send is another command').toBe(3)
    await expect(theirs.locator('li')).toHaveCount(3, { timeout: 15_000 })

    // 6) Caja anula el agua: los dos comanderos lo ven («Anulado en Caja»), sin importes.
    const account = await (await asMain(request, `/checkout/services/${serviceId}`)).json() as { version: number; data: { charges: Charge[] } }
    const water = account.data.charges.find(charge => charge.productId === 'water')!
    expect((await asMain(request, `/checkout/services/${serviceId}/commands/void-charge`, { expectedVersion: account.version, chargeId: water.id, reason: 'equivocado' })).ok()).toBeTruthy()
    await expect(theirs.getByTestId(`had-${water.id}`)).toContainText('Anulado en Caja', { timeout: 15_000 })
    await press(page.getByTestId('back'))
    await expect(page.getByTestId(`had-${water.id}`)).toContainText('Anulado en Caja', { timeout: 15_000 })
    await checkScreen(page, 'what the table has')

    // 7) Salir de la mesa con una ronda sin enviar pide confirmacion; «Seguir con la ronda» no pierde nada; «Salir sin enviar», no envia nada.
    await press(page.getByTestId('entry-bodega'))
    await press(page.getByTestId(`repeat-${ALBARINO.id}-copa`))
    await press(page.getByTestId('back'))                                                      // a la ficha: la ronda sigue, abajo
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
    await press(page.getByTestId('back'))                                                      // a Mesas: se pregunta
    await expect(page.getByTestId('leave-sheet')).toContainText('La ronda tiene 1 sin enviar')
    await checkScreen(page, 'leave with a round')
    await press(page.getByTestId('leave-stay'))
    await expect(page.getByTestId('detail')).toBeVisible()
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
    await press(page.getByTestId('back'))
    await press(page.getByTestId('leave-discard'))
    await expect(page.getByTestId('detail')).toHaveCount(0)
    await expect(tile).toBeVisible()
    expect(await drinkLines(request, serviceId)).toHaveLength(2)                               // el agua anulada no cuenta; nada nuevo

    expect(leaks, 'the PWA received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
  } finally {
    await other.close()
    await release(request, serviceId)
  }
})
