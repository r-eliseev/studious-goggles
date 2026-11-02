<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, type Session } from '../api';

const sessions = ref<Session[]>([]);
const search = ref('');
const error = ref('');
const router = useRouter();
const filtered = computed(() => sessions.value
  .filter(session => session.title.toLocaleLowerCase().includes(search.value.toLocaleLowerCase()))
  .toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt)));

async function refresh() {
  sessions.value = await api.listSessions();
}
async function create() {
  try {
    const session = await api.createSession();
    await refresh();
    await router.push(`/sessions/${session.id}`);
  } catch {
    error.value = 'Could not create a session.';
  }
}
async function rename(session: Session) {
  const title = window.prompt('Session name', session.title)?.trim();
  if (!title || title === session.title) return;
  try {
    await api.renameSession(session.id, title);
    await refresh();
  } catch {
    error.value = 'Could not rename this session.';
  }
}
async function remove(session: Session) {
  if (!window.confirm(`Delete “${session.title}”?`)) return;
  try {
    await api.deleteSession(session.id);
    await refresh();
  } catch {
    error.value = 'Could not delete this session.';
  }
}

onMounted(() => { void refresh().catch(() => { error.value = 'Could not load sessions.'; }); });
</script>

<template>
  <main class="page-shell">
    <header class="page-heading">
      <div><p class="eyebrow">Your workspace</p><h1>Sessions</h1></div>
      <button type="button" @click="create">New session</button>
    </header>
    <label class="search-field" for="session-search">Search sessions</label>
    <input id="session-search" v-model="search" type="search" placeholder="Find by name" />
    <p v-if="error" role="alert">{{ error }}</p>
    <ul class="session-list">
      <li v-for="session in filtered" :key="session.id" class="session-row">
        <RouterLink :to="`/sessions/${session.id}`"><strong>{{ session.title }}</strong><span>{{ new Date(session.updatedAt).toLocaleString() }}</span></RouterLink>
        <div class="row-actions">
          <button type="button" :aria-label="`Rename ${session.title}`" @click="rename(session)">Rename</button>
          <button type="button" :aria-label="`Delete ${session.title}`" @click="remove(session)">Delete</button>
        </div>
      </li>
      <li v-if="filtered.length === 0" class="empty-state">No sessions found.</li>
    </ul>
  </main>
</template>
