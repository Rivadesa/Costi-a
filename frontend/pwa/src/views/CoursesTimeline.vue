<script setup lang="ts">
import { computed } from 'vue'
import type { Names } from '../configuration'
import { coursesTitle } from '../ficha'
import { courseLabel, offerKindLabel } from '../labels'
import { NEXT_MARK, timeline, type TimelineCourse, type TimelineDish } from '../pases'
import type { Dining } from '../types'
import Icon from './Icon.vue'

// C3e (plan 2026-10-27, paso 3; maqueta 2026-10-22, «2 · Sala · Mesa» y «6 · Sala · servido»): la tarjeta «Pases» de la ficha de
// Costina Sala. Solo PINTA lo que da pases.ts (timeline), que agrupa con la regla COMUN de Costina Cocina y del PC:
//  - lo servido y lo omitido, recogido en una linea arriba (courses-served);
//  - cada pase sin terminar, con su nota, sus platos agrupados («Merluza á galega × 4 (1 adaptada)», la estacion por su nombre y el
//    estado) y, bajo cada plato, sus excepciones ESCRITAS y nunca atenuadas: en rojo solo si alarman, en ambar mientras cocina no decide;
//  - el pase «Siguiente» (el que enviaria el boton grande) con el borde del acento, y el listo para servir en verde.
// Ningun boton: la tarjeta solo se lee. Los botones son del pie de la ficha, y existen solo si el servidor los anuncia.
const props = defineProps<{ service: Dining; names: Names; offer: { name: string; kind: string } | null }>()

const line = computed(() => timeline(props.service, stationId => props.names.station(stationId)))
// «Menú de ensayo · Menú degustación»: el nombre de la oferta y su tipo, sin repetir si coinciden.
const offerText = computed(() => props.offer ? [...new Set([props.offer.name, offerKindLabel(props.offer.kind)])].filter(part => part.length > 0).join(' · ') : '')
// La pastilla de estado se calla en el pase «Siguiente» si solo diria «Sin enviar»; un matiz («Falta elegir plato», «Sin platos
// pedidos») se dice siempre, en azul: hay algo que hacer en «Carta y menús».
const nuance = (item: TimelineCourse) => item.state !== courseLabel(item.course.state)
const showState = (item: TimelineCourse) => !item.next || nuance(item)
const stateTone = (item: TimelineCourse) => item.step === 'pending' ? (nuance(item) ? 's-choose' : 's-pending') : `s-${item.step}`
// El plato: rojo si alguna excepcion alarma; ambar si alguna espera a cocina; si no, sin color (el texto lo dice todo).
const dishTone = (dish: TimelineDish) => dish.alarming ? 'alarm' : dish.exceptions.some(exception => exception.pending) ? 'warn' : ''
// «Cocina caliente · Sin enviar»: la estacion ya viene por su NOMBRE (pases.ts) y el estado del plato con las palabras de siempre.
const dishMeta = (dish: TimelineDish) => [dish.station, dish.state].filter(part => part.length > 0).join(' · ')
const lineTone = (exception: TimelineDish['exceptions'][number]) => exception.alarming ? 'alarm' : exception.pending ? 'warn' : 'calm'
</script>

<template>
  <section class="card courses-card fc-tl" data-testid="courses">
    <div class="card-h"><h3>{{ coursesTitle(service) }}</h3><span v-if="offerText" class="muted sm">{{ offerText }}</span></div>
    <p v-if="service.courses.length === 0" class="muted">Esta mesa aún no tiene pases.</p>

    <!-- Lo servido y lo omitido, en una linea: «✓ Aperitivos 20:41», «Postre omitido». -->
    <ul v-if="line.done.length > 0" class="fc-tl-done" data-testid="courses-served">
      <li v-for="item in line.done" :key="item.course.id" :class="{ skipped: item.skipped }" :data-testid="`served-${item.course.id}`">
        <template v-if="item.skipped"><Icon name="skip" :size="15" :stroke="2.2" /><span>{{ item.text }}</span></template>
        <template v-else><Icon name="check" :size="16" :stroke="2.8" /><span class="sr">Servido: </span><b>{{ item.name }}</b> <small v-if="item.time">{{ item.time }}</small></template>
      </li>
    </ul>

    <ol v-if="line.courses.length > 0" class="fc-tl-list">
      <li v-for="item in line.courses" :key="item.course.id" class="fc-tl-pass" :class="{ next: item.next, ready: item.ready }" :data-testid="`course-${item.course.id}`">
        <div class="fc-tl-h">
          <span class="pdot" :class="`st-${item.step}`"><Icon v-if="item.ready" name="bell" :size="16" :stroke="2.4" /><template v-else>{{ item.position }}</template></span>
          <span class="fc-tl-t">
            <b>{{ item.course.name }}</b>
            <small v-if="item.note" :data-testid="`course-note-${item.course.id}`">{{ item.note }}</small>
          </span>
          <span class="fc-tl-pills">
            <span v-if="item.next" class="pill fc-tl-nextpill" data-testid="course-next">{{ NEXT_MARK }}</span>
            <span v-if="showState(item)" class="pill" :class="stateTone(item)">{{ item.state }}</span>
          </span>
        </div>
        <ul v-if="item.dishes.length > 0" class="fc-tl-dishes">
          <li v-for="(dish, index) in item.dishes" :key="dish.dish.key" class="fc-tl-dish" :data-testid="`dish-${item.course.id}-${index}`">
            <div class="fc-tl-dr">
              <span class="fc-tl-dn"><b>{{ dish.dish.name }}</b> <span v-if="dish.note" class="fc-tl-dx" :class="dishTone(dish)">{{ dish.note }}</span></span>
              <span class="fc-tl-q"> × {{ dish.dish.quantity }} </span>
              <small class="fc-tl-dm">{{ dishMeta(dish) }}</small>
            </div>
            <!-- Las excepciones, aparte y bajo su plato: quien, la restriccion escrita y la decision de cocina. Nunca atenuadas. -->
            <ul v-if="dish.exceptions.length > 0" class="fc-tl-xs">
              <li v-for="exception in dish.exceptions" :key="exception.preparation.id" class="fc-tl-x" :class="lineTone(exception)" :data-testid="`prep-${exception.preparation.id}`">
                <Icon :name="exception.alarming || exception.pending ? 'alert' : 'info'" :size="16" :stroke="2.3" /><span>{{ dish.dish.name }} · {{ exception.text }}</span>
              </li>
            </ul>
          </li>
        </ul>
      </li>
    </ol>
  </section>
</template>
