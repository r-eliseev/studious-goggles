import { describe, expect, it, vi } from 'vitest';
import { WebApiClient } from './api';

describe('WebApiClient', () => {
  it('sends same-origin credentials and the antiforgery token on mutations', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(Response.json({ token: 'csrf-token' }))
      .mockResolvedValueOnce(Response.json({ id: 'session-id' }, { status: 201 }));
    const client = new WebApiClient(fetcher);

    await client.createSession();

    expect(fetcher).toHaveBeenNthCalledWith(1, '/api/auth/csrf', expect.objectContaining({ credentials: 'same-origin' }));
    expect(fetcher).toHaveBeenNthCalledWith(2, '/api/sessions', expect.objectContaining({
      method: 'POST',
      credentials: 'same-origin',
    }));
    expect(new Headers(fetcher.mock.calls[1]?.[1]?.headers).get('X-CSRF-TOKEN')).toBe('csrf-token');
  });

  it('reports 401 through the login handler and does not retry a paid POST', async () => {
    const onUnauthorized = vi.fn();
    const fetcher = vi.fn()
      .mockResolvedValueOnce(Response.json({ token: 'csrf-token' }))
      .mockResolvedValueOnce(Response.json({ error: 'unauthorized' }, { status: 401 }));
    const client = new WebApiClient(fetcher, onUnauthorized);

    await expect(client.repeatGeneration('generation-id')).rejects.toMatchObject({ status: 401 });

    expect(onUnauthorized).toHaveBeenCalledOnce();
    expect(fetcher).toHaveBeenCalledTimes(2);
  });
});
