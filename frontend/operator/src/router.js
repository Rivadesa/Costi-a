import { createRouter, createWebHashHistory } from 'vue-router';
import ServiceBoard from './views/ServiceBoard.vue';
import TablesView from './views/TablesView.vue';
import KdsView from './views/KdsView.vue';

export default createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', redirect: '/service' },
    { path: '/service', component: ServiceBoard },
    { path: '/tables', component: TablesView },
    { path: '/kds', component: KdsView },
  ],
});
