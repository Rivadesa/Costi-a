import { lineText, type RoundItem } from './drinks'
import { guestLabel } from './labels'

// C3b: la RONDA de «Bodega y bebidas» (plan 2026-10-15, secciones 2 y 4.4). Espejo de Costina.Client/Round.cs: las dos implementaciones
// recorren la tabla de casos comun dotnet/tests/fixtures/drinks-cases.json.
// Es un BORRADOR EN MEMORIA, nunca una cola: no se guarda en IndexedDB ni en ningun otro sitio (este modulo no importa nada que
// persista; tests/drinks.test.ts lo comprueba), se ve como «Sin enviar» y se pierde al recargar. «Enviar» es UNA sola orden
// add-consumptions con todas las lineas y la version de la mesa leida (F01); mientras haya una orden incierta (D6.7) se puede llenar,
// pero no enviar. Reglas:
//  - un toque suma 1, para toda la mesa; el mismo producto y presentacion para el mismo comensal es la misma linea;
//  - cantidades de 1 a 99 y como mucho 30 lineas (los limites del servidor); el comensal es opcional (null = toda la mesa) y dos lineas
//    que quedan iguales se juntan si la suma no pasa de 99;
//  - mientras va la orden no se toca; al volver: confirmada, la ronda se vacia; sin confirmar, lo enviado ya ES la orden incierta (se ve
//    en su panel y se reintenta identica) y la ronda se vacia para no mandarlo dos veces; rechazada, la ronda sigue entera con el codigo
//    (y la linea, si el servidor la dice); con version_conflict se ofrece «La mesa ha cambiado: enviar de nuevo», que es otra orden,
//    con su clave y la version releida; no salida (no se pudo guardar u otra orden ocupaba el hueco), la ronda sigue igual.

export const MAX_LINES = 30
export const MAX_QUANTITY = 99
export const SEND = 'Enviar'
export const SEND_AGAIN = 'La mesa ha cambiado: enviar de nuevo'

export interface RoundLine extends RoundItem { quantity: number; guestPosition: number | null }
export type AddResult = 'added' | 'more' | 'full' | 'max' | 'sending'
export type Settlement =
  | { kind: 'confirmed' }
  | { kind: 'unconfirmed' }
  | { kind: 'not_sent' }
  | { kind: 'rejected'; code: string; line?: number | null }
export interface RoundSend { body: { expectedVersion: number; lines: Array<{ productId: string; presentationId: string; quantity: number; guestPosition: number | null }> }; description: string }

const isQuantity = (value: number) => Number.isInteger(value) && value >= 1 && value <= MAX_QUANTITY
const sameLine = (a: RoundLine, b: RoundLine) => a.productId === b.productId && a.presentationId === b.presentationId && a.guestPosition === b.guestPosition

export const roundLineText = (line: RoundLine): string => lineText(line.name, line.presentationId, line.presentation)

export class Round {
  lines: RoundLine[] = []
  sending = false
  conflict = false
  problem: { code: string; line: number | null } | null = null

  get units(): number { return this.lines.reduce((total, line) => total + line.quantity, 0) }
  get summary(): string { return this.units === 0 ? '' : `Ronda · ${this.units} sin enviar` }
  get sendLabel(): string { return this.conflict ? SEND_AGAIN : SEND }
  unitsOf(productId: string): number { return this.lines.filter(line => line.productId === productId).reduce((total, line) => total + line.quantity, 0) }

  add(item: RoundItem): AddResult {
    if (this.sending) return 'sending'
    const line = this.lines.find(other => other.productId === item.productId && other.presentationId === item.presentationId && other.guestPosition === null)
    if (line) {
      if (line.quantity >= MAX_QUANTITY) return 'max'
      line.quantity++; this.touched(); return 'more'
    }
    if (this.lines.length >= MAX_LINES) return 'full'
    this.lines.push({ productId: item.productId, presentationId: item.presentationId, name: item.name, presentation: item.presentation, quantity: 1, guestPosition: null })
    this.touched(); return 'added'
  }

  increment(index: number): boolean { const line = this.editable(index); return line !== null && this.setQuantity(index, line.quantity + 1) }

  // En 1, quita la linea (el boton es la papelera).
  decrement(index: number): boolean {
    const line = this.editable(index)
    if (line === null) return false
    return line.quantity <= 1 ? this.remove(index) : this.setQuantity(index, line.quantity - 1)
  }

  remove(index: number): boolean {
    if (this.editable(index) === null) return false
    this.lines.splice(index, 1); this.touched(); return true
  }

  setQuantity(index: number, quantity: number): boolean {
    const line = this.editable(index)
    if (line === null || !isQuantity(quantity)) return false
    line.quantity = quantity; this.touched(); return true
  }

  // null = toda la mesa; un comensal es un numero desde 1 (que exista en la mesa lo comprueba el servidor: 422 con el indice de la linea).
  setGuest(index: number, guestPosition: number | null): boolean {
    const line = this.editable(index)
    if (line === null || guestPosition !== null && !(Number.isInteger(guestPosition) && guestPosition >= 1)) return false
    line.guestPosition = guestPosition
    const twin = this.lines.findIndex((other, at) => at !== index && sameLine(other, line))
    if (twin >= 0 && this.lines[twin].quantity + line.quantity <= MAX_QUANTITY) {
      this.lines[twin].quantity += line.quantity
      this.lines.splice(index, 1)
    }
    this.touched(); return true
  }

  // locked: hay una orden incierta, una bloqueada o otra en curso (D6.7): se puede llenar, pero no enviar.
  canSend(locked: boolean): boolean { return !locked && !this.sending && this.lines.length > 0 }

  // La orden de la ronda entera, con la version de la mesa LEIDA. Desde aqui hasta settle() la ronda no se toca.
  begin(expectedVersion: number, tableName: string): RoundSend | null {
    if (this.sending || this.lines.length === 0) return null
    this.sending = true
    const parts = this.lines.map(line => `${line.quantity} × ${roundLineText(line)}${line.guestPosition === null ? '' : ` (${guestLabel(line.guestPosition)})`}`)
    return {
      body: { expectedVersion, lines: this.lines.map(line => ({ productId: line.productId, presentationId: line.presentationId, quantity: line.quantity, guestPosition: line.guestPosition })) },
      description: `Ronda para ${tableName}: ${parts.join(', ')}`,
    }
  }

  settle(result: Settlement): void {
    if (!this.sending) return
    this.sending = false
    if (result.kind === 'confirmed' || result.kind === 'unconfirmed') { this.lines = []; this.conflict = false; this.problem = null; return }
    if (result.kind === 'rejected') {
      this.conflict = result.code === 'version_conflict'
      this.problem = { code: result.code, line: result.line ?? null }
    }
  }

  clear(): void { if (!this.sending) { this.lines = []; this.touched() } }

  private editable(index: number): RoundLine | null { return this.sending || !Number.isInteger(index) ? null : this.lines[index] ?? null }
  // Cualquier cambio retira el aviso del ultimo rechazo: su indice de linea ya puede ser otro y la orden siguiente sera otra.
  private touched(): void { this.conflict = false; this.problem = null }
}
