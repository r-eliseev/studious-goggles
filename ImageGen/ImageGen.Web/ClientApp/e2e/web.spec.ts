import { expect, test, type Page, type Route } from '@playwright/test';

const session = {
  id: 'session-1',
  title: 'Test session',
  createdAt: '2026-11-01T00:00:00Z',
  updatedAt: '2026-11-02T00:00:00Z',
  settings: {
    imageModelIds: [],
    assistantModelId: { providerId: 'openrouter', id: 'assistant' },
    runsPerModel: 1,
    parameters: {},
  },
};
const timeline = {
  session,
  messages: [],
  runs: [],
  generations: [],
};

async function mockApi(page: Page) {
  let authenticated = false;
  let messages = timeline.messages;
  let runs = timeline.runs;
  let generations = timeline.generations;
  let savedKey = false;
  let sends = 0;
  await page.route('**/api/**', async (route: Route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname === '/api/auth/me') {
      return authenticated ? route.fulfill({ json: { authenticated: true, user: 'owner' } })
        : route.fulfill({ status: 401, json: { error: 'unauthorized' } });
    }
    if (url.pathname === '/api/auth/login' && method === 'POST') {
      authenticated = true;
      return route.fulfill({ json: { authenticated: true } });
    }
    if (url.pathname === '/api/auth/csrf') return route.fulfill({ json: { token: 'csrf-test' } });
    if (url.pathname === '/api/auth/logout') {
      authenticated = false;
      return route.fulfill({ json: { authenticated: false } });
    }
    if (url.pathname === '/api/sessions' && method === 'GET') return route.fulfill({ json: [session] });
    if (url.pathname === '/api/sessions' && method === 'POST') return route.fulfill({ status: 201, json: session });
    if (url.pathname === '/api/sessions/session-1/timeline') {
      return route.fulfill({ json: { session, messages, runs, generations } });
    }
    if (url.pathname === '/api/sessions/session-1/messages' && method === 'POST') {
      sends += 1;
      messages = [{ id: 'message-1', role: 'User', text: 'A red fox', createdAt: '2026-11-02T00:00:01Z', runId: 'run-1', isError: false, errorKind: null, costUsd: null }];
      runs = [{ id: 'run-1', sessionId: 'session-1', prompt: 'A red fox', origin: 'User', createdAt: '2026-11-02T00:00:02Z' }];
      generations = [{ id: 'generation-1', runId: 'run-1', sessionId: 'session-1', providerId: 'fal', modelId: 'flux', prompt: 'A red fox', seed: 42, status: 'Succeeded', imagePath: 'images/test.png', mediaType: 'image/png', costUsd: null, errorKind: null, createdAt: '2026-11-02T00:00:03Z', completedAt: '2026-11-02T00:00:04Z', sortOrder: 0 }];
      return route.fulfill({ json: { user: messages[0], assistant: null, run: runs[0] } });
    }
    if (url.pathname === '/api/models/image') return route.fulfill({ json: [] });
    if (url.pathname === '/api/models/assistant') return route.fulfill({ json: [] });
    if (url.pathname === '/api/providers' && method === 'GET') return route.fulfill({ json: [{ id: 'openrouter', displayName: 'OpenRouter', kind: 'Image', maskedKey: savedKey ? '••••1234' : null }] });
    if (url.pathname === '/api/providers/openrouter/key' && method === 'PUT') {
      savedKey = true;
      return route.fulfill({ json: { status: 'Valid' } });
    }
    if (url.pathname === '/api/events') {
      return route.fulfill({ contentType: 'text/event-stream', body: 'event: generation\ndata: {"generationId":"generation-1","sessionId":"session-1","status":"Succeeded"}\n\n' });
    }
    return route.fulfill({ status: 404, json: { error: 'not_found' } });
  });
  return { getSends: () => sends };
}

test('login return, chat update, direct route, and narrow screen work without provider calls', async ({ page }) => {
  const apiState = await mockApi(page);
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto('/sessions/session-1');
  await expect(page.getByLabel('Web password')).toBeVisible();
  await page.getByLabel('Web password').fill('test-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Test session' })).toBeVisible();
  await page.getByLabel('Message').fill('A red fox');
  await page.getByRole('button', { name: 'Send' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Ready' })).toBeVisible();
  await expect.poll(apiState.getSends).toBe(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Test session' })).toBeVisible();
});

test('provider key input clears after submission and is not stored in browser storage', async ({ page }) => {
  await mockApi(page);
  await page.goto('/login');
  await page.getByLabel('Web password').fill('test-password');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.getByRole('link', { name: 'Settings' }).click();
  const key = page.getByLabel('New OpenRouter key');
  await key.fill('private-test-key');
  await page.getByRole('button', { name: 'Save key' }).click();
  await expect(key).toHaveValue('');
  expect(await page.evaluate(() => JSON.stringify({ local: Object.values(localStorage), session: Object.values(sessionStorage) }))).not.toContain('private-test-key');
});
