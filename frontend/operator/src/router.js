import { createRouter, createWebHashHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import ServiceBoard from './views/ServiceBoard.vue';
import ServiceDetail from './views/ServiceDetail.vue';
import KdsView from './views/KdsView.vue';
import CheckoutView from './views/CheckoutView.vue';
import { DEMO_API_BASE } from './api/demo.js';
import { hasPermission, isAuthenticated, session } from './state/session.js';

function defaultTerminalPath() {
  return session.terminalMode === 'kds' ? '/kds' : '/service';
}

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/service' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/service', component: ServiceBoard, meta: { terminals: ['main', 'service'] } },
    { path: '/service/:id', component: ServiceDetail, meta: { terminals: ['main', 'service'] } },
    { path: '/kds', component: KdsView, meta: { terminals: ['main', 'kds'] } },
    { path: '/checkout', component: CheckoutView, meta: { permission: 'payment.record', realServer: true, terminals: ['main'] } },
    { path: '/:pathMatch(.*)*', redirect: '/service' },
  ],
});

router.beforeEach((to) => {
  if (!to.meta.public && !isAuthenticated.value) return '/login';
  if (to.path === '/login' && isAuthenticated.value) return defaultTerminalPath();
  if (Array.isArray(to.meta.terminals) && !to.meta.terminals.includes(session.terminalMode)) return defaultTerminalPath();
  if (to.meta.realServer && session.apiBase === DEMO_API_BASE) return defaultTerminalPath();
  if (to.meta.permission && !hasPermission(to.meta.permission)) return defaultTerminalPath();
  return true;
});

export default router;
