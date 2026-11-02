<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { api, type ImageModel, type Timeline } from '../api';
import { mergeGenerationEvent, subscribeSessionEvents } from '../events';

const route = useRoute();
const sessionId = String(route.params.id);
const timeline = ref<Timeline | null>(null);
const text = ref('');
const files = ref<File[]>([]);
const imageModels = ref<ImageModel[]>([]);
const otherModelIds = ref<Record<string, string>>({});
const busy = ref(false);
const error = ref('');
let closeEvents: (() => void) | undefined;

const entries = computed(() => {
  if (!timeline.value) return [];
  return [
    ...timeline.value.messages.map(message => ({ type: 'message' as const, at: message.createdAt, message })),
    ...timeline.value.runs.map(run => ({ type: 'run' as const, at: run.createdAt, run })),
  ].sort((left, right) => left.at.localeCompare(right.at));
});

async function refresh() {
  timeline.value = await api.timeline(sessionId);
}
async function send() {
  if ((!text.value.trim() && files.value.length === 0) || busy.value) return;
  busy.value = true;
  error.value = '';
  try {
    await api.sendMessage(sessionId, text.value, files.value);
    text.value = '';
    files.value = [];
    const fileInput = document.querySelector<HTMLInputElement>('#attachments');
    if (fileInput) fileInput.value = '';
    await refresh();
  } catch {
    error.value = 'Could not send the message.';
  } finally {
    busy.value = false;
  }
}
async function generationAction(action: () => Promise<unknown>) {
  try {
    await action();
    await refresh();
  } catch {
    error.value = 'The generation action could not be completed.';
  }
}
function onFiles(event: Event) {
  files.value = Array.from((event.target as HTMLInputElement).files ?? []);
}
function price(cost: number | null) { return cost === null ? 'Price unknown' : `$${cost.toFixed(4)}`; }
function generationImage(id: string) { return `/api/generations/${encodeURIComponent(id)}/image`; }
function editPrompt(prompt: string) { return window.prompt('Edit prompt', prompt); }
async function editGeneration(generationId: string, prompt: string) {
  const edited = editPrompt(prompt);
  if (edited !== null && edited.trim()) await generationAction(() => api.editGeneration(generationId, edited));
}
function statusLabel(status: string) {
  return ({ Queued: 'Queued', Running: 'Generating', Succeeded: 'Ready', Failed: 'Failed', Filtered: 'Filtered', Cancelled: 'Cancelled' } as Record<string, string>)[status] ?? status;
}
function selectModel(model: ImageModel) { return `${model.providerId}/${model.id}`; }
async function useOtherModel(generationId: string) {
  const selected = otherModelIds.value[generationId];
  const model = imageModels.value.find(item => selectModel(item) === selected);
  if (model) await generationAction(() => api.otherModel(generationId, selectModel(model)));
}
function copyPrompt(prompt: string) { void navigator.clipboard.writeText(prompt); }

onMounted(async () => {
  try {
    await Promise.all([refresh(), api.imageModels().then(models => { imageModels.value = models; })]);
    closeEvents = subscribeSessionEvents(sessionId, event => {
      if (timeline.value) timeline.value.generations = mergeGenerationEvent(timeline.value.generations, event, sessionId);
    }, () => { void refresh().catch(() => { error.value = 'Could not refresh session state.'; }); });
  } catch {
    error.value = 'Could not load this session.';
  }
});
onUnmounted(() => closeEvents?.());
</script>

<template>
  <main class="page-shell chat-shell">
    <header class="page-heading">
      <div><RouterLink to="/">← Sessions</RouterLink><p class="eyebrow">Conversation</p><h1>{{ timeline?.session.title ?? 'Loading session…' }}</h1></div>
      <RouterLink v-if="timeline" :to="`/sessions/${sessionId}/models`">Models</RouterLink>
    </header>
    <p v-if="error" role="alert">{{ error }}</p>
    <section class="timeline" aria-label="Conversation">
      <template v-for="entry in entries" :key="entry.type === 'message' ? `message-${entry.message.id}` : `run-${entry.run.id}`">
        <article v-if="entry.type === 'message'" class="message" :class="`message-${entry.message.role.toLowerCase()}`">
          <h2>{{ entry.message.role === 'User' ? 'You' : 'Assistant' }}</h2>
          <p>{{ entry.message.text }}</p>
          <span v-if="entry.message.costUsd !== null" class="subtle">{{ price(entry.message.costUsd) }}</span>
        </article>
        <article v-else class="run-card">
          <header><h2>{{ entry.run.prompt }}</h2><button type="button" @click="copyPrompt(entry.run.prompt)">Copy prompt</button></header>
          <div class="generation-grid">
            <section v-for="generation in timeline?.generations.filter(item => item.runId === entry.run.id).toSorted((a, b) => a.sortOrder - b.sortOrder)" :key="generation.id" class="generation-card">
              <img v-if="generation.status === 'Succeeded'" :src="generationImage(generation.id)" :alt="`Generated image from ${generation.modelId}`" />
              <p class="generation-status" role="status">{{ statusLabel(generation.status) }}</p>
              <p>{{ generation.modelId }} · {{ price(generation.costUsd) }}</p>
              <p v-if="generation.errorKind" role="alert">{{ generation.errorKind }}</p>
              <a v-if="generation.status === 'Succeeded'" :href="generationImage(generation.id)" download>Save image</a>
              <div class="row-actions">
                <button type="button" @click="generationAction(() => api.repeatGeneration(generation.id))">Repeat</button>
                <button type="button" @click="generationAction(() => api.newSeed(generation.id))">New seed</button>
                <button type="button" @click="editGeneration(generation.id, generation.prompt)">Edit</button>
              </div>
              <div class="other-model">
                <label :for="`model-${generation.id}`">Other model</label>
                <select :id="`model-${generation.id}`" v-model="otherModelIds[generation.id]">
                  <option value="">Choose a model</option>
                  <option v-for="model in imageModels" :key="selectModel(model)" :value="selectModel(model)">{{ model.name }}</option>
                </select>
                <button type="button" :disabled="!otherModelIds[generation.id]" @click="useOtherModel(generation.id)">Generate</button>
              </div>
            </section>
          </div>
        </article>
      </template>
      <p v-if="entries.length === 0 && timeline" class="empty-state">Start with a prompt for your assistant.</p>
    </section>
    <form class="composer" @submit.prevent="send">
      <label for="message">Message</label>
      <textarea id="message" v-model="text" rows="3" placeholder="Describe what you want to create"></textarea>
      <label for="attachments">Attach images</label>
      <input id="attachments" type="file" accept="image/png,image/jpeg,image/webp" multiple @change="onFiles" />
      <button type="submit" :disabled="busy || (!text.trim() && files.length === 0)">{{ busy ? 'Sending…' : 'Send' }}</button>
    </form>
  </main>
</template>
