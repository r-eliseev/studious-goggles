<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { api, type Provider } from '../api';
import ProviderKeysView from '../components/ProviderKeysView.vue';

const providers = ref<Provider[]>([]);
const error = ref('');
const darkTheme = ref(localStorage.getItem('imagegen.theme') !== 'light');
async function loadProviders() {
  providers.value = await api.providers();
}
async function saveKey(id: string, key: string) {
  await api.saveProviderKey(id, key);
  await loadProviders();
}
async function removeKey(id: string) {
  await api.removeProviderKey(id);
  await loadProviders();
}
function setTheme() {
  document.documentElement.dataset.theme = darkTheme.value ? 'dark' : 'light';
  localStorage.setItem('imagegen.theme', darkTheme.value ? 'dark' : 'light');
}
onMounted(() => {
  setTheme();
  void loadProviders().catch(() => { error.value = 'Could not load providers.'; });
});
</script>

<template>
  <main class="page-shell">
    <h1>Settings</h1>
    <p v-if="error" role="alert">{{ error }}</p>
    <label class="choice-row"><input v-model="darkTheme" type="checkbox" @change="setTheme" /> Dark theme</label>
    <ProviderKeysView :providers="providers" :save-key="saveKey" :remove-key="removeKey" />
  </main>
</template>
