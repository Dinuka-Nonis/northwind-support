import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { getTickets: vi.fn() },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

/** Long enough for the first load to have happened, however it is scheduled. */
const INITIAL_LOAD_MS = 300;

/** Lets timers and the promises they resolve settle. */
const settle = (ms: number) =>
  act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });

describe('useTicketList requests (DEFECT-117)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('requests immediately when the status changes, without waiting for a search', async () => {
    const { result } = renderHook(() => useTicketList());
    await settle(INITIAL_LOAD_MS);
    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => result.current.updateFilters({ status: 'InProgress' }));
    await settle(0);

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'InProgress' }));
  });

  it.each<[string, Partial<TicketFilters>]>([
    ['priority', { priority: 'High' }],
    ['SLA status', { slaStatus: 'AtRisk' }],
    ['category', { categoryId: 3 }],
    ['customer', { customerId: 2 }],
    ['assigned agent', { assignedAgentId: 4 }],
    ['unassigned only', { unassignedOnly: true }],
    ['sort order', { sortBy: 'dueAtUtc', sortDirection: 'asc' }],
  ])('requests immediately when the %s changes', async (_name, patch) => {
    const { result } = renderHook(() => useTicketList());
    await settle(INITIAL_LOAD_MS);

    act(() => result.current.updateFilters(patch));
    await settle(0);

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
  });

  it('keeps the status filter when the search settles, and keeps working after the search is cleared', async () => {
    const { result } = renderHook(() => useTicketList());
    await settle(INITIAL_LOAD_MS);

    act(() => result.current.updateFilters({ status: 'InProgress' }));
    await settle(0);
    act(() => result.current.updateFilters({ search: 'invoice' }));
    await settle(300);

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
    );

    act(() => result.current.updateFilters({ search: '' }));
    await settle(300);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', search: '' }),
    );

    // The third symptom in the report: after clearing the search, filters went stale again.
    act(() => result.current.updateFilters({ status: 'Resolved' }));
    await settle(0);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Resolved' }));
  });

  it('sends one search request for a burst of keystrokes', async () => {
    const { result } = renderHook(() => useTicketList());
    await settle(INITIAL_LOAD_MS);
    getTickets.mockClear();

    for (const text of ['i', 'in', 'inv', 'invo']) {
      act(() => result.current.updateFilters({ search: text }));
      await settle(100);
    }
    expect(getTickets).not.toHaveBeenCalled();

    await settle(300);
    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenCalledWith(expect.objectContaining({ search: 'invo' }));
  });

  it('goes back to page 1 in the same request as a new search', async () => {
    const { result } = renderHook(() => useTicketList());
    await settle(INITIAL_LOAD_MS);

    act(() => result.current.updateFilters({ page: 3 }));
    await settle(0);
    getTickets.mockClear();

    act(() => result.current.updateFilters({ search: 'invoice' }));
    await settle(300);

    expect(getTickets).toHaveBeenCalledTimes(1);
    expect(getTickets).toHaveBeenCalledWith(expect.objectContaining({ search: 'invoice', page: 1 }));
  });
});