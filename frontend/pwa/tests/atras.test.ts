import { describe, expect, it } from 'vitest'
import { BackStack, browserHistory, STATE, type HistoryLike } from '../src/atras'

// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01): «Atrás» del sistema baja un nivel en Costina Sala. Sin DOM: el historial
// es un doble que apunta cada pushState y cada back.

function fake(): HistoryLike & { calls: string[]; popstate: () => void } {
  const calls: string[] = []
  return { calls, pushState: () => calls.push('push'), back: () => calls.push('back'), popstate: () => undefined }
}

describe('each open level leaves one history entry; closing it from the screen consumes it', () => {
  it('enter pushes, exit goes back and the popstate that follows is ignored', () => {
    const history = fake(), back = new BackStack(history)
    back.enter()
    expect([history.calls, back.open]).toEqual([['push'], 1])
    back.exit()
    expect([history.calls, back.open]).toEqual([['push', 'back'], 0])
    expect(back.pop()).toBe('ignored')                                                          // el popstate de ese back()
    expect(back.pop()).toBe('none')                                                             // ya no hay nada nuestro
  })
  it('exit with nothing open does nothing', () => {
    const history = fake(), back = new BackStack(history)
    back.exit()
    expect([history.calls, back.open]).toEqual([[], 0])
  })
})

describe('a popstate from the person closes the top level', () => {
  it('ours while there are levels open; none when the app is at the floor plan', () => {
    const history = fake(), back = new BackStack(history)
    back.enter(); back.enter()
    expect(back.pop()).toBe('ours'); expect(back.open).toBe(1)
    expect(back.pop()).toBe('ours'); expect(back.open).toBe(0)
    expect(back.pop()).toBe('none')
    expect(history.calls).toEqual(['push', 'push'])                                             // un pop nuestro no toca el historial
  })
  it('a level that asks before closing puts its entry back (keep); confirming later consumes it as always', () => {
    const history = fake(), back = new BackStack(history)
    back.enter()
    expect(back.pop()).toBe('ours')
    back.keep()
    expect([history.calls, back.open]).toEqual([['push', 'push'], 1])
    back.exit()                                                                                 // la persona confirmo: la pantalla cierra el nivel
    expect([history.calls, back.open]).toEqual([['push', 'push', 'back'], 0])
    expect(back.pop()).toBe('ignored')
  })
  it('exits and pops interleave without losing count: two screen closes, then one back from the person', () => {
    const history = fake(), back = new BackStack(history)
    back.enter(); back.enter(); back.enter()
    back.exit(); back.exit()
    expect(back.pop()).toBe('ignored'); expect(back.pop()).toBe('ignored')
    expect(back.pop()).toBe('ours'); expect(back.open).toBe(0)
  })
})

describe('the browser history never changes the address', () => {
  it('pushes a state with our mark and the same URL (no url argument)', () => {
    const pushed: unknown[][] = [], backs: number[] = []
    const scope = globalThis as unknown as { history?: unknown }
    const before = scope.history
    scope.history = { pushState: (...args: unknown[]) => { pushed.push(args) }, back: () => { backs.push(1) } }
    try {
      const history = browserHistory()
      history.pushState(); history.back()
      expect(pushed).toEqual([[STATE, '']])
      expect(backs).toEqual([1])
    } finally { scope.history = before }
  })
})
