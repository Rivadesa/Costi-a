import { createRouter, createWebHashHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import ServiceBoard from './views/ServiceBoard.vue';
import ServiceDetail from './views/ServiceDetail.vue';
import KdsView from './views/KdsView.vue';
import CheckoutView from './views/CheckoutView.vue';
import { DEMO_API_BASE } from './api/demo.js';
import { hasPermission, isAuthenticated, session } from './state/session.js';

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/service' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/service', component: ServiceBoard },
    { path: '/service/:id', component: ServiceDetail },
    { path: '/kds', component: KdsView },
    { path: '/checkout', component: CheckoutView, meta: { permission: 'payment.record', realServer: true } },
    { path: '/:pathMatch(.*)*', redirect: '/service' },
  ],
});

router.beforeEach((to) => {
  if (!to.meta.public && !isAuthenticated.value) return '/login';
  if (to.path === '/login' && isAuthenticated.value) return '/service';
  if (to.meta.realServer && session.apiBase === DEMO_API_BASE) return '/service';
  if (to.meta.permission && !hasPermission(to.meta.permission)) return '/service';
  return true;
});

export default router;
