import { useCallback, useEffect, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketFilters } from '../../../types/api';

/** How long the search box must be idle before its text becomes a request. */
const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [searchText, setSearchText] = useState(emptyFilters.search);

  // Commit the typed search into `filters` after DEBOUNCE_MS of quiet. The cleanup runs on
  // every keystroke, cancelling the previous commit — this is the debounce. Functional update
  // so a filter change made while typing is preserved rather than overwritten.
  useEffect(() => {
    if (searchText.trim() === filters.search.trim()) {
      return;
    }

    const handle = window.setTimeout(
      () => setFilters((current) => ({ ...current, search: searchText, page: 1 })),
      DEBOUNCE_MS,
    );

    return () => window.clearTimeout(handle);
  }, [searchText, filters.search]);

  const load = useCallback(() => ticketsApi.getTickets(filters), [filters]);
  const { reload, ...state } = useAsyncData(load, 'Unable to load tickets.');

  const updateFilters = useCallback((patch: Partial<TicketFilters>) => {
    // Split the patch: search goes to display state (committed by the effect above),
    // everything else is request-relevant and commits immediately.
    const { search, ...rest } = patch;

    if (search !== undefined) {
      setSearchText(search);
    }

    // Any committed filter change invalidates the current page, except an explicit page change.
    if (Object.keys(rest).length > 0) {
      setFilters((current) => ({ ...current, ...rest, page: rest.page ?? 1 }));
    }
  }, []);

  const resetFilters = useCallback(() => {
    setSearchText(emptyFilters.search);
    setFilters(emptyFilters);
  }, []);

  return {
    // The input reads `filters.search`, so it must show live text — not the last committed query.
    filters: { ...filters, search: searchText },
    updateFilters,
    resetFilters,
    reload,
    isLoading: state.isLoading,
    data: state.data,
    error: state.error,
  };
}