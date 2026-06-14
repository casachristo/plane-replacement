import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TransitionMenu } from './TransitionMenu';

vi.mock('next/navigation', () => ({ useRouter: () => ({ refresh: vi.fn() }) }));

const states = [
  { id: 's1', name: 'To Do', color: '#aaaaaa' },
  { id: 's2', name: 'In Progress', color: '#bbbbbb' },
  { id: 's3', name: 'Done', color: '#cccccc' },
] as any;

function mockFetch(impl: (...a: any[]) => any) {
  global.fetch = vi.fn(impl) as any;
}

describe('TransitionMenu', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('renders a button for every state except the current one', () => {
    render(<TransitionMenu projectSlug="p" issueSeq={1} currentStateId="s1" states={states} />);
    expect(screen.queryByRole('button', { name: /To Do/ })).toBeNull();
    expect(screen.getByRole('button', { name: /In Progress/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Done/ })).toBeInTheDocument();
  });

  it('POSTs the chosen state id to the transitions endpoint', async () => {
    mockFetch(async () => ({ ok: true, status: 200, json: async () => ({}) }));
    render(<TransitionMenu projectSlug="proj" issueSeq={7} currentStateId="s1" states={states} />);
    await userEvent.click(screen.getByRole('button', { name: /Done/ }));
    expect(global.fetch).toHaveBeenCalledWith(
      '/api/v1/projects/proj/issues/7/transitions',
      expect.objectContaining({ method: 'POST', body: JSON.stringify({ toStateId: 's3' }) }),
    );
  });

  it('renders the override form with the unchecked criteria when the gate returns 412', async () => {
    mockFetch(async () => ({
      ok: false,
      status: 412,
      json: async () => ({ error: { details: { unchecked: [{ id: 'a', position: 0, text: 'write tests' }] } } }),
    }));
    render(<TransitionMenu projectSlug="proj" issueSeq={7} currentStateId="s1" states={states} />);
    await userEvent.click(screen.getByRole('button', { name: /Done/ }));
    expect(await screen.findByText(/acceptance criteria unchecked/i)).toBeInTheDocument();
    expect(screen.getByText('write tests')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Override and move/i })).toBeInTheDocument();
  });

  it('surfaces a generic error when the transition fails', async () => {
    mockFetch(async () => ({ ok: false, status: 500, json: async () => ({}) }));
    render(<TransitionMenu projectSlug="proj" issueSeq={7} currentStateId="s1" states={states} />);
    await userEvent.click(screen.getByRole('button', { name: /Done/ }));
    expect(await screen.findByText(/Failed \(500\)/)).toBeInTheDocument();
  });
});
