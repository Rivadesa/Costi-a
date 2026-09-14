import { onBeforeUnmount, onMounted, ref } from 'vue';

export function usePolling(loader, intervalMs = 3000) {
  const loading = ref(false);
  const error = ref(null);
  let timer = null;
  let stopped = false;

  async function refresh() {
    if (loading.value || stopped) return;
    loading.value = true;
    try {
      await loader();
      error.value = null;
    } catch (err) {
      error.value = err;
    } finally {
      loading.value = false;
    }
  }

  onMounted(async () => {
    stopped = false;
    await refresh();
    timer = window.setInterval(refresh, intervalMs);
  });

  onBeforeUnmount(() => {
    stopped = true;
    if (timer) window.clearInterval(timer);
  });

  return { refresh, loading, error };
}
