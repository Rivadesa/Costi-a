// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01): el «Atrás» del sistema (el gesto o el boton de Android; «<» de Safari)
// baja UN nivel en Costina Sala en vez de salir de la aplicacion: de Carta o Bodega a la ficha, y de la ficha o de «Abrir mesa» al plano,
// con la MISMA pregunta que «Mesas» si hay una ronda o una alergia sin enviar. Sin DOM: el historial llega como dependencia (vitest).
//  - Cada nivel que la pantalla abre deja UNA entrada en el historial (enter). Cerrarlo desde la pantalla («Mesas», la flecha) consume esa
//    entrada (exit: history.back(), y el popstate que provoca se ignora).
//  - Un popstate que no es de un exit es el «Atrás» de la persona: si hay niveles abiertos es nuestro (pop → 'ours') y la pantalla cierra
//    el de arriba; si no hay ninguno ('none'), el navegador hace lo suyo (salir, o volver a la pagina anterior).
//  - Si el nivel de arriba no se cierra sin preguntar (una ronda sin enviar, una alergia escrita), la entrada que el navegador ya quito se
//    REPONE (keep) para que la aplicacion siga donde esta; al confirmar, cerrar el nivel la consume como siempre.
// La direccion no cambia nunca (pushState con la misma): nada recarga ni navega. Una recarga olvida la cuenta: las entradas que queden
// en el historial son del mismo documento y un «Atrás» sobre ellas no hace nada visible hasta agotarlas.
export interface HistoryLike { pushState(): void; back(): void }

export class BackStack {
  private depth = 0     // niveles abiertos con su entrada en el historial
  private ignored = 0   // popstate que provocara un exit() pendiente de llegar
  constructor(private readonly history: HistoryLike) {}
  get open(): number { return this.depth }
  // La pantalla abrio un nivel.
  enter(): void { this.history.pushState(); this.depth++ }
  // La pantalla cerro un nivel por su cuenta: se consume su entrada. El popstate que sigue no es de la persona.
  exit(): void {
    if (this.depth === 0) return
    this.depth--; this.ignored++
    this.history.back()
  }
  // Llego un popstate.
  pop(): 'ours' | 'ignored' | 'none' {
    if (this.ignored > 0) { this.ignored--; return 'ignored' }
    if (this.depth === 0) return 'none'
    this.depth--
    return 'ours'
  }
  // El nivel de arriba pregunta antes de cerrarse: la entrada vuelve al historial.
  keep(): void { this.history.pushState(); this.depth++ }
}

export const STATE = { costina: 'nivel' } as const
export const browserHistory = (): HistoryLike => ({
  pushState: () => history.pushState(STATE, ''),
  back: () => history.back(),
})
