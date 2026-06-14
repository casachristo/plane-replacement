import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TokenManager } from './TokenManager';

function mockFetch(impl: (...a: any[]) => any) {
  global.fetch = vi.fn(impl) as any;
}

const existing = [
  { id: 't1', name: 'cairn', prefix: 'abcd1234', kind: 'Service', scopes: ['issue:read'], revokedAt: null },
] as any;

describe('TokenManager', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('creates a token and shows the one-time secret from the API response', async () => {
    const calls: any[] = [];
    mockFetch(async (url: string, opts: any) => {
      calls.push({ url, method: opts?.method });
      if (opts?.method === 'POST') return { ok: true, status: 201, json: async () => ({ fullToken: 'wpt_one_time_secret' }) };
      return { ok: true, status: 200, json: async () => [] };
    });
    render(<TokenManager initial={[]} />);
    await userEvent.type(screen.getByPlaceholderText(/cairn-shadow/), 'my-agent');
    await userEvent.click(screen.getByRole('button', { name: /Create token/i }));

    expect(await screen.findByText('wpt_one_time_secret')).toBeInTheDocument();
    expect(calls.some(c => c.url === '/api/admin/tokens/' && c.method === 'POST')).toBe(true);
  });

  it('refuses an empty name without calling the API', async () => {
    mockFetch(async () => ({ ok: true, status: 200, json: async () => [] }));
    render(<TokenManager initial={[]} />);
    await userEvent.click(screen.getByRole('button', { name: /Create token/i }));
    expect(await screen.findByText(/Name is required/i)).toBeInTheDocument();
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('revokes a token via DELETE after confirmation', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const calls: any[] = [];
    mockFetch(async (url: string, opts: any) => {
      calls.push({ url, method: opts?.method });
      return { ok: true, status: 200, json: async () => [] };
    });
    render(<TokenManager initial={existing} />);
    await userEvent.click(screen.getByRole('button', { name: /^revoke$/i }));
    expect(calls.some(c => c.url === '/api/admin/tokens/t1' && c.method === 'DELETE')).toBe(true);
  });
});
