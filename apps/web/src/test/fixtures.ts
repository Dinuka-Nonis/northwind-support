import type {
  EscalationResult,
  PagedResult,
  TicketDetail,
  TicketEscalation,
  TicketListItem,
} from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

/** A ticket as the detail screen receives it. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'The invoice says 120.00 but the order was 100.00.',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard', email: 'ops@fabrikam.test', phone: null },
    category: { id: 2, name: 'Billing', requiresSpecialist: false },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

/** One escalation history row: Medium to High, Alex to Ben. */
export function escalationRow(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromDueAtUtc: '2026-09-25T08:00:00Z',
    toDueAtUtc: '2026-09-25T18:00:00Z',
    fromAgent: { id: 1, fullName: 'Alex Turner' },
    toAgent: { id: 2, fullName: 'Ben Osei' },
    reason: 'Customer chased twice',
    escalatedBy: 'Priya Nair',
    escalatedAtUtc: '2026-09-25T10:00:00Z',
    ...overrides,
  };
}

/** What POST /escalate returns for a High ticket that goes to Critical. */
export function escalationResult(overrides: Partial<EscalationResult> = {}): EscalationResult {
  return {
    ticket: ticketDetail({ priority: 'Critical' }),
    escalation: escalationRow({ fromPriority: 'High', toPriority: 'Critical' }),
    slaReason: 'Critical priority: 4 h window.',
    assignmentReason: 'Kept Sara Lindqvist: still active and qualified.',
    ...overrides,
  };
}
