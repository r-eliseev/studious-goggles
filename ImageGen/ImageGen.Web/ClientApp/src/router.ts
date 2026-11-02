import { createRouter, createWebHistory } from 'vue-router';
import { ApiError, api } from './api';
import ChatView from './views/ChatView.vue';
import LoginView from './views/LoginView.vue';
import ModelsView from './views/ModelsView.vue';
import SessionsView from './views/SessionsView.vue';
import SettingsView from './views/SettingsView.vue';

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView, meta: { public: true } },
    { path: '/', name: 'sessions', component: SessionsView },
    { path: '/sessions/:id', name: 'chat', component: ChatView },
    { path: '/sessions/:id/models', name: 'models', component: ModelsView },
    { path: '/settings', name: 'settings', component: SettingsView },
  ],
});

router.beforeEach(async to => {
  if (to.meta.public) return true;
  try {
    await api.me();
    return true;
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return { name: 'login', query: { returnTo: to.fullPath } };
    }
    return true;
  }
});
