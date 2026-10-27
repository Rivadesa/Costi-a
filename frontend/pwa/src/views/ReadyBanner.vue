<script setup lang="ts">
import { computed } from 'vue'
import type { Names } from '../configuration'
import { moreText, noticeSince, noticeTitle, READY_TEXT, serveLabel, serveRequest, type ReadyNotice } from '../sala'
import Icon from './Icon.vue'

// C3c: la banda «Mesa 4 · Principal listo para servir» de Costina Sala (maqueta «5 · Sala · aviso»). Va en BoardView debajo de la
// cabecera, en CUALQUIER pantalla (plano, ficha, «Abrir mesa», Carta, Bodega), y ocupa su sitio: no tapa volver, el buscador ni la
// bandeja de la ronda, porque se queda hasta «Servido» o «Visto». Si hay varias, la mas antigua arriba y «+N más». Solo PRESENTACION:
// que pases salen lo decide sala.ts con lo que anuncia el servidor; «Servido» existe solo si el servidor anuncia serve en ese pase y
// la orden la manda BoardView (la orden incierta de siempre). Los data-testid de la banda no empiezan por «do-»: la ficha de la misma
// mesa puede estar debajo con su propio boton de servir (en Carta o Bodega; C3e: con la ficha de esa mesa a la vista, BoardView no le
// pasa su aviso, porque el boton grande ya dice «Servido · Primero»).
const props = defineProps<{ notices: ReadyNotice[]; names: Names; locked: boolean }>()
const emit = defineEmits<{ serve: [notice: ReadyNotice]; view: [notice: ReadyNotice]; seen: [notice: ReadyNotice] }>()

const first = computed(() => props.notices[0] ?? null)
const rest = computed(() => props.notices.slice(1))
const table = (tableId: string) => props.names.table(tableId)
const title = computed(() => first.value ? noticeTitle(first.value, table(first.value.entry.service.tableId)) : '')
const since = computed(() => first.value ? noticeSince(first.value) : '')
const canServe = computed(() => first.value !== null && serveRequest(first.value, title.value) !== null)
const more = computed(() => moreText(rest.value, table))
const label = computed(() => first.value ? serveLabel(first.value) : '')
function act(event: 'serve' | 'view' | 'seen'): void {
  const notice = first.value
  if (notice === null) return
  if (event === 'serve') emit('serve', notice)
  else if (event === 'view') emit('view', notice)
  else emit('seen', notice)
}
</script>

<template>
  <section v-if="first" class="banner" role="alert" data-testid="ready-banner">
    <div class="bn-h">
      <span class="bn-ic" aria-hidden="true"><Icon name="bell" :size="22" :stroke="2.2" /></span>
      <p class="bn-t"><b data-testid="banner-title">{{ title }}</b><span>{{ READY_TEXT }}</span><small v-if="since">{{ since }}</small></p>
    </div>
    <button v-if="canServe" type="button" class="bn-serve" :disabled="locked" data-testid="banner-serve" @click="act('serve')">
      <Icon name="check" :size="22" :stroke="2.6" />{{ label }}</button>
    <div class="bn-row">
      <button type="button" class="bn-x" data-testid="banner-view" @click="act('view')">Ver mesa<Icon name="right" :size="18" :stroke="2.4" /></button>
      <button type="button" class="bn-x" data-testid="banner-seen" @click="act('seen')">Visto</button>
    </div>
    <p v-if="rest.length > 0" class="bn-more" data-testid="banner-more"><b>+{{ rest.length }} más:</b> {{ more }}</p>
  </section>
</template>
