import { describe, expect, it } from 'vitest'
import { offerKindSuffix, proposedOffer } from '../src/offers'

// E4d: el comandero propone el menu del dia (la primera oferta vigente) aunque haya varias; sin ofertas, ninguna.
describe('menu del dia en el comandero', () => {
  it('proposes the first offer even when there are several', () => {
    expect(proposedOffer([{ id: 'LAB-TASTING' }, { id: 'LAB-DAILY' }, { id: 'LAB-CARTA' }])?.id).toBe('LAB-TASTING')
    expect(proposedOffer([{ id: 'ONLY' }])?.id).toBe('ONLY')
    expect(proposedOffer([])).toBeNull()
  })
  it('labels the kind with the plan words and leaves the tasting menu bare', () => {
    expect(offerKindSuffix('tasting')).toBe('')
    expect(offerKindSuffix('set-menu')).toBe(' (con elección)')
    expect(offerKindSuffix('a-la-carte')).toBe(' (carta)')
  })
})
