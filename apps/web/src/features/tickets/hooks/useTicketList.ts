import { useCallback, useEffect, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketFilters } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
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
    const { search, ...rest } = patch;

    if (search !== undefined) {
      setSearchText(search);
    }

    if (Object.keys(rest).length > 0) {
      setFilters((current) => ({ ...current, ...rest, page: rest.page ?? 1 }));
    }
  }, []);

  const resetFilters = useCallback(() => {
    setSearchText(emptyFilters.search);
    setFilters(emptyFilters);
  }, []);

  return {
    // The box shows what the user typed, not the last committed search.
    filters: { ...filters, search: searchText },
    updateFilters,
    resetFilters,
    reload,
    isLoading: state.isLoading,
    data: state.data,
    error: state.error,
  };
}
