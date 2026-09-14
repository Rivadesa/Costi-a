<script setup>
defineProps({ status: { type: String, required: true } });

const labels = {
  open: 'Abierta', in_service: 'En servicio', paused: 'Pausada', pending_payment: 'Pendiente de cobro',
  paid: 'Cobrada', closed: 'Cerrada', cancelled: 'Anulada', pending: 'Pendiente', fired: 'Enviada',
  preparing: 'Preparando', ready: 'Lista', served: 'Servida', skipped: 'Omitida',
};
</script>

<template>
  <span class="status-pill" :data-status="status">{{ labels[status] || status }}</span>
</template>
