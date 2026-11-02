<script setup lang="ts">
import { ref } from 'vue';
import type { Provider } from '../api';

defineProps<{ providers: Provider[]; saveKey: (id: string, key: string) => Promise<void>; removeKey?: (id: string) => Promise<void> }>();
const keys = ref<Record<string, string>>({});
const errors = ref<Record<string, string>>({});
const saving = ref<Record<string, boolean>>({});

async function submit(provider: Provider, saveKey: (id: string, key: string) => Promise<void>) {
  const key = keys.value[provider.id] ?? '';
  if (!key.trim()) return;
  saving.value[provider.id] = true;
  errors.value[provider.id] = '';
  try {
    await saveKey(provider.id, key);
  } catch {
    errors.value[provider.id] = 'Could not save provider key.';
  } finally {
    keys.value[provider.id] = '';
    saving.value[provider.id] = false;
  }
}
</script>

<template>
  <section aria-labelledby="provider-keys-title">
    <h2 id="provider-keys-title">Provider keys</h2>
    <article v-for="provider in providers" :key="provider.id" class="provider-key">
      <div>
        <h3>{{ provider.displayName }}</h3>
        <p v-if="provider.maskedKey">Saved key: {{ provider.maskedKey }}</p>
        <p v-else>No key saved</p>
      </div>
      <form @submit.prevent="submit(provider, saveKey)">
        <label :for="`key-${provider.id}`">New {{ provider.displayName }} key</label>
        <input :id="`key-${provider.id}`" v-model="keys[provider.id]" type="password" autocomplete="new-password" />
        <button type="submit" :disabled="saving[provider.id] || !keys[provider.id]">Save key</button>
        <button v-if="provider.maskedKey && removeKey" type="button" @click="removeKey(provider.id)">Remove key</button>
        <p v-if="errors[provider.id]" role="alert">{{ errors[provider.id] }}</p>
      </form>
    </article>
  </section>
</template>
