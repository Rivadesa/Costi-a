<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref, watch } from 'vue'
import { initials, PHOTOS } from '../photos'

// C3b: la foto de una bebida (E3.2), o un recuadro con sus iniciales si no la hay o falla. Solo se pide cuando la fila se ve
// (IntersectionObserver, con un margen para que llegue antes de verse) y solo si su huella no esta ya en memoria (photos.ts). Los bytes
// llegan con el token y se pintan con una URL blob: (la CSP solo gana img-src blob:). Decorativa: el nombre ya esta escrito en la fila.
const props = withDefaults(defineProps<{ productId: string; imageSha: string | null; name: string; size?: 'sm' | 'md' }>(), { size: 'md' })
const cache = inject(PHOTOS, null)
const root = ref<HTMLElement | null>(null)
const url = ref<string | null>(cache?.cached(props.imageSha) ?? null)
const broken = ref(false)
let observer: IntersectionObserver | null = null
let seen = false
let retried = false

async function load(): Promise<void> {
  const sha = props.imageSha
  if (cache === null || !sha) { url.value = null; return }
  const found = await cache.load(props.productId, sha)
  if (props.imageSha === sha) { url.value = found; broken.value = false }
}
function observe(): void {
  if (cache === null || !props.imageSha || url.value !== null) return
  if (typeof IntersectionObserver === 'undefined') { seen = true; void load(); return }
  observer ??= new IntersectionObserver(entries => {
    if (!entries.some(entry => entry.isIntersecting)) return
    seen = true; observer?.disconnect(); observer = null
    void load()
  }, { rootMargin: '240px 0px' })
  if (root.value) observer.observe(root.value)
}
// Una URL ya liberada (la memoria descarto esa foto) falla al pintarse otra vez: se pide de nuevo una vez; si vuelve a fallar, iniciales.
function failed(): void {
  if (retried) { broken.value = true; return }
  retried = true; url.value = null
  void load()
}
onMounted(observe)
onUnmounted(() => { observer?.disconnect(); observer = null })
watch(() => props.imageSha, () => {
  url.value = cache?.cached(props.imageSha) ?? null; broken.value = false; retried = false
  if (url.value === null) { if (seen) void load(); else observe() }
})
</script>

<template>
  <span ref="root" class="thumb" :class="size" aria-hidden="true">
    <img v-if="url !== null && !broken" :src="url" alt="" decoding="async" draggable="false" data-testid="photo" @error="failed" />
    <span v-else class="initials">{{ initials(name) }}</span>
  </span>
</template>
