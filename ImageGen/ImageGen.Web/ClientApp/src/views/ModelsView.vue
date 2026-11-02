<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { api, type AssistantModel, type ImageModel, type Session } from '../api';

const route = useRoute();
const sessionId = String(route.params.id);
const session = ref<Session | null>(null);
const images = ref<ImageModel[]>([]);
const assistants = ref<AssistantModel[]>([]);
const error = ref('');
const saved = ref(false);

async function load() {
  const [timeline, imageCatalog, assistantCatalog] = await Promise.all([api.timeline(sessionId), api.imageModels(), api.assistantModels()]);
  session.value = timeline.session;
  images.value = imageCatalog;
  assistants.value = assistantCatalog;
}
function imageKey(model: { providerId: string; id: string }) { return `${model.providerId}/${model.id}`; }
async function save() {
  if (!session.value) return;
  error.value = '';
  saved.value = false;
  try {
    await api.updateSettings(sessionId, session.value.settings);
    saved.value = true;
  } catch {
    error.value = 'Could not save model settings.';
  }
}
onMounted(() => { void load().catch(() => { error.value = 'Could not load model catalogs.'; }); });
</script>

<template>
  <main class="page-shell">
    <RouterLink :to="`/sessions/${sessionId}`">← Back to chat</RouterLink>
    <h1>Models</h1>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="saved" role="status">Settings saved.</p>
    <template v-if="session">
      <section>
        <h2>Image models</h2>
        <label v-for="model in images" :key="imageKey(model)" class="choice-row">
          <input v-model="session.settings.imageModelIds" type="checkbox" :value="{ providerId: model.providerId, id: model.id }" />
          <span><strong>{{ model.name }}</strong><small>{{ model.providerId }} · {{ model.prices.length ? model.prices.map(price => `$${price.costUsd}/${price.unit}`).join(', ') : 'Price unknown' }}</small></span>
        </label>
      </section>
      <section>
        <h2>Assistant model</h2>
        <label for="assistant-model">Choose assistant model</label>
        <select id="assistant-model" v-model="session.settings.assistantModelId">
          <option v-for="model in assistants" :key="imageKey(model)" :value="{ providerId: model.providerId, id: model.id }">{{ model.name }} ({{ model.providerId }})</option>
        </select>
      </section>
      <button type="button" @click="save">Save model settings</button>
    </template>
  </main>
</template>
