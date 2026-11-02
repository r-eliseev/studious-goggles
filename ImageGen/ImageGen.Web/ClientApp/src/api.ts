export type SessionSettings = {
  imageModelIds: string[];
  assistantModelId: string;
  runsPerModel: number;
  parameters: { aspectRatio: string | null; resolution: string | null; outputFormat: string | null; quality: string | null };
};

export type Session = {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  settings: SessionSettings;
};

export type Provider = { id: string; displayName: string; kind: string; maskedKey: string | null };
export type ImageModel = { providerId: string; id: string; name: string; supportsSeed: boolean; supportsImageInput: boolean; maxImagesPerRequest: number; prices: { billable: string; unit: string; costUsd: number; variant: string | null }[] };
export type AssistantModel = { providerId: string; id: string; name: string; supportsImageInput: boolean; contextLength: number | null; supportsTools: boolean };
export type Timeline = {
  session: Session;
  messages: { id: string; role: string; text: string; createdAt: string; runId: string | null; isError: boolean; errorKind: string | null; costUsd: number | null }[];
  runs: { id: string; sessionId: string; prompt: string; origin: string; createdAt: string }[];
  generations: { id: string; runId: string; sessionId: string; providerId: string; modelId: string; prompt: string; seed: number; status: string; imagePath: string | null; mediaType: string | null; costUsd: number | null; errorKind: string | null; createdAt: string; completedAt: string | null; sortOrder: number }[];
};

export class ApiError extends Error {
  constructor(readonly status: number, readonly code: string) {
    super(code);
    this.name = 'ApiError';
  }
}

type Fetcher = typeof fetch;

export class WebApiClient {
  private csrfToken: string | null = null;

  constructor(
    private readonly fetcher: Fetcher = fetch,
    private readonly onUnauthorized: () => void = () => undefined,
  ) {}

  async login(password: string): Promise<void> {
    await this.request('/api/auth/login', { method: 'POST', body: JSON.stringify({ password }), headers: { 'Content-Type': 'application/json' } }, false, false);
    this.csrfToken = null;
  }

  async logout(): Promise<void> {
    await this.request('/api/auth/logout', { method: 'POST' }, true);
    this.csrfToken = null;
  }

  async me(): Promise<{ authenticated: boolean; user: string }> {
    return this.request('/api/auth/me');
  }

  async listSessions(): Promise<Session[]> { return this.request('/api/sessions'); }
  async createSession(): Promise<Session> { return this.request('/api/sessions', { method: 'POST' }, true); }
  async renameSession(id: string, title: string): Promise<Session> {
    return this.request(`/api/sessions/${encodeURIComponent(id)}`, { method: 'PATCH', body: JSON.stringify({ title }), headers: { 'Content-Type': 'application/json' } }, true);
  }
  async deleteSession(id: string): Promise<void> {
    await this.request(`/api/sessions/${encodeURIComponent(id)}`, { method: 'DELETE' }, true);
  }
  async timeline(id: string): Promise<Timeline> { return this.request(`/api/sessions/${encodeURIComponent(id)}/timeline`); }
  async imageModels(): Promise<ImageModel[]> { return this.request('/api/models/image'); }
  async assistantModels(): Promise<AssistantModel[]> { return this.request('/api/models/assistant'); }
  async providers(): Promise<Provider[]> { return this.request('/api/providers'); }
  async saveProviderKey(id: string, apiKey: string): Promise<void> {
    await this.request(`/api/providers/${encodeURIComponent(id)}/key`, { method: 'PUT', body: JSON.stringify({ apiKey }), headers: { 'Content-Type': 'application/json' } }, true);
  }
  async removeProviderKey(id: string): Promise<void> {
    await this.request(`/api/providers/${encodeURIComponent(id)}/key`, { method: 'DELETE' }, true);
  }
  async updateSettings(id: string, settings: SessionSettings): Promise<Session> {
    return this.request(`/api/sessions/${encodeURIComponent(id)}/settings`, { method: 'PUT', body: JSON.stringify(settings), headers: { 'Content-Type': 'application/json' } }, true);
  }
  async sendMessage(id: string, text: string, files: File[]): Promise<unknown> {
    const body = new FormData();
    body.set('text', text);
    for (const file of files) body.append('files', file);
    return this.request(`/api/sessions/${encodeURIComponent(id)}/messages`, { method: 'POST', body }, true);
  }
  async repeatGeneration(id: string): Promise<unknown> { return this.request(`/api/generations/${encodeURIComponent(id)}/repeat`, { method: 'POST' }, true); }
  async newSeed(id: string): Promise<unknown> { return this.request(`/api/generations/${encodeURIComponent(id)}/new-seed`, { method: 'POST' }, true); }
  async editGeneration(id: string, prompt: string): Promise<unknown> {
    return this.request(`/api/generations/${encodeURIComponent(id)}/edit`, { method: 'POST', body: JSON.stringify({ prompt }), headers: { 'Content-Type': 'application/json' } }, true);
  }
  async otherModel(id: string, model: string): Promise<unknown> {
    return this.request(`/api/generations/${encodeURIComponent(id)}/other-model`, { method: 'POST', body: JSON.stringify({ model }), headers: { 'Content-Type': 'application/json' } }, true);
  }

  private async request<T = void>(path: string, init: RequestInit = {}, mutation = false, handleUnauthorized = true): Promise<T> {
    const headers = new Headers(init.headers);
    if (mutation && path !== '/api/auth/login') {
      headers.set('X-CSRF-TOKEN', await this.getCsrfToken());
    }
    const response = await this.fetcher(path, { ...init, headers, credentials: 'same-origin' });
    if (response.status === 401) {
      if (handleUnauthorized && path !== '/api/auth/login') this.onUnauthorized();
      const payload = await response.json().catch(() => null) as { error?: string } | null;
      throw new ApiError(401, payload?.error ?? 'unauthorized');
    }
    if (!response.ok) {
      const payload = await response.json().catch(() => null) as { error?: string } | null;
      throw new ApiError(response.status, payload?.error ?? 'request_failed');
    }
    if (response.status === 204) return undefined as T;
    return await response.json() as T;
  }

  private async getCsrfToken(): Promise<string> {
    if (this.csrfToken) return this.csrfToken;
    const response = await this.fetcher('/api/auth/csrf', { credentials: 'same-origin' });
    if (response.status === 401) {
      this.onUnauthorized();
      throw new ApiError(401, 'unauthorized');
    }
    if (!response.ok) throw new ApiError(response.status, 'csrf_unavailable');
    const payload = await response.json() as { token: string };
    this.csrfToken = payload.token;
    return payload.token;
  }
}

export const api = new WebApiClient(fetch, () => {
  if (window.location.pathname !== '/login') {
    const returnTo = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    window.location.assign(`/login?returnTo=${encodeURIComponent(returnTo)}`);
  }
});
