// C3a: iconos de trazo del comandero, los de la maqueta aprobada (docs/plans/maquetas/2026-10-15-comandero.html) y del mismo dibujo
// que Icons.xaml del puesto: 24 x 24, sin relleno, extremos redondeados. Se pintan como SVG DENTRO de las plantillas (Icon.vue): no
// son imagenes ni fuentes, asi que la CSP del motor no cambia. Solo trazados <path>: un circulo es un arco cerrado.
const circle = (cx: number, cy: number, r: number) => `M${cx - r} ${cy}a${r} ${r} 0 1 0 ${2 * r} 0a${r} ${r} 0 1 0 ${-2 * r} 0`

export const ICONS = {
  back: ['M15 5l-7 7 7 7'],
  right: ['M9 6l6 6-6 6'],
  down: ['M6 9l6 6 6-6'],
  up: ['M6 15l6-6 6 6'],
  fork: ['M7 3v8', 'M4 3v5a3 3 0 0 0 6 0V3', 'M7 11v10', 'M18 3c-2 1.5-3 4-3 7h3v11'],
  glass: ['M7 3h10v4a5 5 0 0 1-10 0z', 'M12 12v8', 'M8 21h8'],
  alert: ['M12 3 2 20h20z', 'M12 10v4', 'M12 17h.01'],
  check: ['M5 12.5l4.5 4.5L19 7'],
  checkCircle: [circle(12, 12, 9), 'M8 12.5l2.8 2.8L16 10'],
  send: ['M21 3 3 10.5l7 2.5 2.5 7z', 'M10 13 21 3'],
  clock: [circle(12, 12, 9), 'M12 7v5l3 2'],
  dots: ['M5 12h.01', 'M12 12h.01', 'M19 12h.01'],
  pause: ['M9 5v14', 'M15 5v14'],
  play: ['M7 5l12 7-12 7z'],
  skip: ['M6 5l9 7-9 7z', 'M18 5v14'],
  flag: ['M5 21V4', 'M5 4h11l-2 4 2 4H5'],
  door: ['M4 21h16', 'M6 21V4h10v17', 'M13 12h.01'],
  x: ['M6 6l12 12', 'M18 6 6 18'],
  plus: ['M12 5v14', 'M5 12h14'],
  minus: ['M5 12h14'],
  users: [circle(9, 8, 3), 'M3 20a6 6 0 0 1 12 0', 'M16 5a3 3 0 0 1 0 6', 'M18 20a6 6 0 0 0-3-5.2'],
  refresh: ['M20 11a8 8 0 1 0-2.3 5.7', 'M20 4v7h-7'],
  wifi: ['M4.5 9.5a11 11 0 0 1 15 0', 'M7.5 13a6.5 6.5 0 0 1 9 0', 'M12 17h.01'],
  wifiOff: ['M4.5 9.5a11 11 0 0 1 15 0', 'M7.5 13a6.5 6.5 0 0 1 9 0', 'M12 17h.01', 'M3 3l18 18'],
  mobile: ['M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z', 'M11 18h2'],
  chef: ['M7 14a4 4 0 1 1 2.2-7.4A4 4 0 0 1 17 7a4 4 0 0 1 0 7', 'M7 14v6h10v-6', 'M7 17h10'],
  info: [circle(12, 12, 9), 'M12 11v5', 'M12 8h.01'],
  grid: ['M4 4h7v7H4z', 'M13 4h7v7h-7z', 'M4 13h7v7H4z', 'M13 13h7v7h-7z'],
  // C3b: el buscador de «Bodega y bebidas» y la papelera de la ronda.
  search: [circle(11, 11, 7), 'M20 20l-4-4'],
  trash: ['M4 7h16', 'M9 7V4h6v3', 'M6 7l1 13h10l1-13', 'M10 11v6', 'M14 11v6'],
  // C3c: «Compartir» de Safari (cuadro con flecha hacia arriba), para explicar como anadir el icono en iPhone y iPad.
  share: ['M12 15V3', 'M8 7l4-4 4 4', 'M8 10H5v11h14V10h-3'],
  // C3c: la campana del aviso «listo para servir» de Costina Sala (maqueta «5 · Sala · aviso»).
  bell: ['M6 16v-5a6 6 0 0 1 12 0v5l2 2H4z', 'M10 21h4'],
} as const satisfies Record<string, readonly string[]>

export type IconName = keyof typeof ICONS
