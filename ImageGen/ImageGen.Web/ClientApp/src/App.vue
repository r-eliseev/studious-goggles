<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { api } from './api';
import { router } from './router';

const route = router.currentRoute;
const busy = ref(false);
const vueRouter = useRouter();
async function signOut() {
  if (busy.value) return;
  busy.value = true;
  try {
    await api.logout();
    await vueRouter.replace('/login');
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div class="app-frame">
    <nav v-if="route.name !== 'login'" class="top-nav" aria-label="Main navigation">
      <RouterLink class="brand" to="/">ImageGen</RouterLink>
      <div class="nav-links">
        <RouterLink to="/">Sessions</RouterLink>
        <RouterLink to="/settings">Settings</RouterLink>
        <button type="button" :disabled="busy" @click="signOut">Sign out</button>
      </div>
    </nav>
    <RouterView />
  </div>
</template>
