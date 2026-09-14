import { createRouter, createWebHashHistory } from 'vue-router';
import LoginView from './views/LoginView.vue';
import ServiceBoard from './views/ServiceBoard.vue';
import ServiceDetail from './views/ServiceDetail.vue';
import KdsView from './views/KdsView.vue';
import { isAuthenticated } from './state/session.js';

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/service' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/service', component: ServiceBoard },
    { path: '/service/:id', component: ServiceDetail },
    { path: '/kds', component: KdsView },
    { path: '/:pathMatch(.*)*', redirect: '/service' },
  ],
});

router.beforeEach((to) => {
  if (!to.meta.public && !isAuthenticated.value) return '/login';
  if (to.path === '/login' && isAuthenticated.value) return '/service';
  return true;
});

export default router;
