<script setup lang="ts">
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { api, ApiError } from '../api';

const password = ref('');
const error = ref('');
const busy = ref(false);
const route = useRoute();
const router = useRouter();

async function submit() {
  if (busy.value) return;
  busy.value = true;
  error.value = '';
  try {
    await api.login(password.value);
    password.value = '';
    const returnTo = typeof route.query.returnTo === 'string' && route.query.returnTo.startsWith('/') && !route.query.returnTo.startsWith('//')
      ? route.query.returnTo
      : '/';
    await router.replace(returnTo);
  } catch (failure) {
    error.value = failure instanceof ApiError && failure.status === 401 ? 'Incorrect password.' : 'Could not sign in. Try again.';
    password.value = '';
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <main class="login-page">
    <h1>ImageGen</h1>
    <form @submit.prevent="submit">
      <label for="password">Web password</label>
      <input id="password" v-model="password" type="password" autocomplete="current-password" required />
      <p v-if="error" role="alert">{{ error }}</p>
      <button type="submit" :disabled="busy || !password">{{ busy ? 'Signing in…' : 'Sign in' }}</button>
    </form>
  </main>
</template>
