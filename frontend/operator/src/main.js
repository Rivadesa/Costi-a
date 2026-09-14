import { createApp } from 'vue';
import App from './App.vue';
import router from './router.js';
import { restoreSession } from './api/client.js';
import './style.css';
import './operator-extra.css';

await restoreSession();
createApp(App).use(router).mount('#app');
